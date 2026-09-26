"""In-memory registry for connected Unity plugin sessions."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone

import asyncio

from core.config import config
from models.models import ToolDefinitionModel


@dataclass(slots=True)
class PluginSession:
    """Represents a single Unity plugin connection."""

    session_id: str
    project_name: str
    project_hash: str
    unity_version: str
    registered_at: datetime
    connected_at: datetime
    tools: dict[str, ToolDefinitionModel] = field(default_factory=dict)
    project_id: str | None = None
    # Full path to project root (for focus nudging)
    project_path: str | None = None
    user_id: str | None = None  # Associated user id (None for local mode)


class PluginRegistry:
    """Stores active plugin sessions in-memory.

    The registry is optimised for quick lookup by either ``session_id`` or
    ``project_hash`` (which is used as the canonical "instance id" across the
    HTTP command routing stack).

    In remote-hosted mode, sessions are scoped by (user_id, project_hash) composite key
    to ensure session isolation between users.
    """

    def __init__(self) -> None:
        self._sessions: dict[str, PluginSession] = {}
        # In local mode: project_hash -> session_id
        # In remote mode: (user_id, project_hash) -> session_id
        self._hash_to_session: dict[str, str] = {}
        self._user_hash_to_session: dict[tuple[str, str], str] = {}
        self._lock = asyncio.Lock()

    async def register(
        self,
        session_id: str,
        project_name: str,
        project_hash: str,
        unity_version: str,
        project_path: str | None = None,
        user_id: str | None = None,
    ) -> tuple[PluginSession, str | None]:
        """Register (or replace) a plugin session.

        If an existing session already claims the same ``project_hash`` (and ``user_id``
        in remote-hosted mode) it will be replaced, ensuring that reconnect scenarios
        always map to the latest WebSocket connection.

        Returns:
            A tuple of (new_session, evicted_session_id). The evicted ID is None
            when no previous session was replaced.
        """
        if config.http_remote_hosted and not user_id:
            raise ValueError("user_id is required in remote-hosted mode")

        async with self._lock:
            now = datetime.now(timezone.utc)
            session = PluginSession(
                session_id=session_id,
                project_name=project_name,
                project_hash=project_hash,
                unity_version=unity_version,
                registered_at=now,
                connected_at=now,
                project_path=project_path,
                user_id=user_id,
            )

            # Remove old mapping for this hash if it existed under a different session
            evicted_session_id: str | None = None
            if user_id:
                # Remote-hosted mode: use composite key (user_id, project_hash)
                composite_key = (user_id, project_hash)
                previous_session_id = self._user_hash_to_session.get(
                    composite_key)
                if previous_session_id and previous_session_id != session_id:
                    self._sessions.pop(previous_session_id, None)
                    evicted_session_id = previous_session_id
                self._user_hash_to_session[composite_key] = session_id
            else:
                # Local mode: use project_hash only
                previous_session_id = self._hash_to_session.get(project_hash)
                if previous_session_id and previous_session_id != session_id:
                    self._sessions.pop(previous_session_id, None)
                    evicted_session_id = previous_session_id
                self._hash_to_session[project_hash] = session_id

            self._sessions[session_id] = session
            return session, evicted_session_id

    async def touch(self, session_id: str) -> None:
        """Update the ``connected_at`` timestamp when a heartbeat is received."""

        async with self._lock:
            session = self._sessions.get(session_id)
            if session:
                session.connected_at = datetime.now(timezone.utc)

    async def unregister(self, session_id: str) -> None:
        """Remove a plugin session from the registry."""

        async with self._lock:
            session = self._sessions.pop(session_id, None)
            if session:
                # Clean up hash mappings
                if session.project_hash in self._hash_to_session:
                    mapped = self._hash_to_session.get(session.project_hash)
                    if mapped == session_id:
                        del self._hash_to_session[session.project_hash]

                # Clean up user-scoped mappings
                if session.user_id:
                    composite_key = (session.user_id, session.project_hash)
                    if composite_key in self._user_hash_to_session:
                        mapped = self._user_hash_to_session.get(composite_key)
                        if mapped == session_id:
                            del self._user_hash_to_session[composite_key]

    async def cleanup_stale(self, max_age_seconds: float) -> list[str]:
        """Evict sessions whose last liveness signal is older than ``max_age_seconds``.

        A Unity plugin that dies without sending a WebSocket close frame (process
        crash, network drop, OS sleep) never reaches :meth:`unregister`, so its
        session stays in ``_sessions`` forever: ``get_sessions()`` reports a ghost
        instance and :meth:`get_session_id_by_hash` still resolves a routing target
        whose WebSocket is dead.

        ``PluginHub._ping_loop`` normally catches this, but it is per-session and has
        escape hatches — it starts only once that session registers, it needs
        PING_TIMEOUT to elapse, and it dies with its task (e.g. across a domain
        reload) without a fallback. This sweep is the registry-level net underneath
        it.

        Liveness is ``connected_at``, which :meth:`touch` advances on every heartbeat
        (including the plugin's own pongs), so a healthy-but-idle session is never
        evicted — only one that has genuinely stopped signalling.

        Returns the evicted session ids so the caller can log or tear down the
        matching transports.
        """
        if max_age_seconds <= 0:
            raise ValueError("max_age_seconds must be positive")

        now = datetime.now(timezone.utc)
        # Snapshot under the lock, unregister outside it: unregister() re-acquires
        # the same non-reentrant asyncio.Lock, so calling it inside would deadlock.
        async with self._lock:
            stale_ids = [
                session_id
                for session_id, session in self._sessions.items()
                if (now - session.connected_at).total_seconds() > max_age_seconds
            ]

        for session_id in stale_ids:
            await self.unregister(session_id)
        return stale_ids

    async def register_tools_for_session(self, session_id: str, tools: list[ToolDefinitionModel]) -> None:
        """Register tools for a specific session."""
        async with self._lock:
            session = self._sessions.get(session_id)
            if session:
                # Replace existing tools or merge? Usually replace for "set state".
                # We will replace the dict but keep the field.
                session.tools.clear()
                for tool in tools:
                    session.tools[tool.name] = tool

    async def get_session(self, session_id: str) -> PluginSession | None:
        """Fetch a session by its ``session_id``."""

        async with self._lock:
            return self._sessions.get(session_id)

    async def get_session_id_by_hash(self, project_hash: str, user_id: str | None = None) -> str | None:
        """Resolve a ``project_hash`` (Unity instance id) to a session id."""

        if user_id:
            async with self._lock:
                return self._user_hash_to_session.get((user_id, project_hash))
        else:
            async with self._lock:
                return self._hash_to_session.get(project_hash)

    async def list_sessions(self, user_id: str | None = None) -> dict[str, PluginSession]:
        """Return a shallow copy of sessions.

        Args:
            user_id: If provided, only return sessions for this user (remote-hosted mode).
                     If None, return all sessions (local mode only).

        Raises:
            ValueError: If ``user_id`` is None while running in remote-hosted mode.
                        This prevents accidentally leaking sessions across users.
        """
        if user_id is None and config.http_remote_hosted:
            raise ValueError(
                "list_sessions requires user_id in remote-hosted mode"
            )

        async with self._lock:
            if user_id is None:
                return dict(self._sessions)
            else:
                return {
                    sid: session
                    for sid, session in self._sessions.items()
                    if session.user_id == user_id
                }


__all__ = ["PluginRegistry", "PluginSession"]
