"""Regression tests for issue #109 — orphaned plugin sessions had no TTL.

The failure being pinned: a Unity plugin that dies without a WebSocket close frame
never reaches :meth:`PluginRegistry.unregister`, so its session stays in the registry
forever. That is observable three ways, and each is asserted below:

* ``list_sessions()`` reports an instance that no longer exists (ghost instance in
  ``mcpforunity://instances``),
* ``get_session_id_by_hash()`` still resolves a routing target whose WebSocket is
  dead, and
* the session object (plus its tools dict) is never reclaimed.

Opposite-direction guards are asserted too, because a TTL that evicts *anything* is
as wrong as one that evicts nothing: a session touched recently must survive, and a
session refreshed by a heartbeat must survive past its original registration age.
"""

from datetime import datetime, timedelta, timezone

import pytest

from transport.plugin_registry import PluginRegistry


async def _register(registry: PluginRegistry, session_id: str = "sess-1", **kwargs):
    """Register a session with sane defaults and return it."""
    session, evicted = await registry.register(
        session_id,
        kwargs.pop("project_name", "MyProject"),
        kwargs.pop("project_hash", "hash1"),
        kwargs.pop("unity_version", "2022.3"),
        **kwargs,
    )
    assert evicted is None
    return session


def _age_session(registry: PluginRegistry, session_id: str, seconds: float) -> None:
    """Backdate a session's liveness stamp, simulating elapsed wall-clock time.

    ``cleanup_stale`` reads ``connected_at`` via ``datetime.now(timezone.utc)``, so
    backdating the stamp is equivalent to the real clock advancing while leaving the
    test synchronous — no sleeps, no flake.
    """
    registry._sessions[session_id].connected_at = datetime.now(timezone.utc) - timedelta(
        seconds=seconds
    )


class TestCleanupStale:
    @pytest.mark.asyncio
    async def test_stale_session_is_evicted_and_reported(self):
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 300)

        evicted = await registry.cleanup_stale(max_age_seconds=120)

        assert evicted == ["sess-1"]

    @pytest.mark.asyncio
    async def test_eviction_clears_all_three_observable_surfaces(self):
        """The ghost is gone from the session map, the hash map, and the listing."""
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 300)

        await registry.cleanup_stale(max_age_seconds=120)

        assert await registry.get_session("sess-1") is None, "session object still held"
        assert await registry.get_session_id_by_hash("hash1") is None, (
            "routing still resolves a session whose WebSocket is dead"
        )
        assert await registry.list_sessions() == {}, "ghost instance still listed"

    @pytest.mark.asyncio
    async def test_recent_session_survives(self):
        """Opposite direction: a TTL must not evict a live session."""
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 5)

        evicted = await registry.cleanup_stale(max_age_seconds=120)

        assert evicted == []
        assert await registry.get_session("sess-1") is not None

    @pytest.mark.asyncio
    async def test_touch_extends_life(self):
        """A heartbeat resets the clock, so a long-lived healthy session is never reaped.

        This is the case that separates 'stale' from 'merely old': the session was
        registered well before the threshold, but its last signal is recent.
        """
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 10_000)
        await registry.touch("sess-1")

        evicted = await registry.cleanup_stale(max_age_seconds=120)

        assert evicted == [], "a touched session was evicted as stale"

    @pytest.mark.asyncio
    async def test_only_the_stale_session_is_evicted(self):
        registry = PluginRegistry()
        await _register(registry, "sess-old", project_hash="hash-old")
        await _register(registry, "sess-new", project_hash="hash-new")
        _age_session(registry, "sess-old", 300)

        evicted = await registry.cleanup_stale(max_age_seconds=120)

        assert evicted == ["sess-old"]
        assert await registry.get_session("sess-new") is not None
        assert await registry.get_session_id_by_hash("hash-new") == "sess-new"

    @pytest.mark.asyncio
    async def test_eviction_releases_user_scoped_mapping(self):
        """Remote-hosted mode keys on (user_id, project_hash); that map must clear too."""
        registry = PluginRegistry()
        await _register(registry, user_id="user-A")
        _age_session(registry, "sess-1", 300)

        await registry.cleanup_stale(max_age_seconds=120)

        assert await registry.get_session_id_by_hash("hash1", "user-A") is None
        assert registry._user_hash_to_session == {}

    @pytest.mark.asyncio
    async def test_second_sweep_is_a_no_op(self):
        """Idempotence: the sweep runs every 60s forever, so a re-sweep must not re-report."""
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 300)

        first = await registry.cleanup_stale(max_age_seconds=120)
        second = await registry.cleanup_stale(max_age_seconds=120)

        assert first == ["sess-1"]
        assert second == []

    @pytest.mark.asyncio
    async def test_empty_registry_returns_empty(self):
        registry = PluginRegistry()

        assert await registry.cleanup_stale(max_age_seconds=120) == []

    @pytest.mark.asyncio
    async def test_non_positive_threshold_is_rejected(self):
        """A zero/negative TTL would evict every live session — fail loudly, not silently.

        Guarded because the sweep loop passes a class constant; a misconfigured
        override must not turn 'reap orphans' into 'reap everyone on the next tick'.
        """
        registry = PluginRegistry()
        await _register(registry)

        with pytest.raises(ValueError, match="must be positive"):
            await registry.cleanup_stale(max_age_seconds=0)

        assert await registry.get_session("sess-1") is not None, (
            "the rejected call must not have evicted anything"
        )

    @pytest.mark.asyncio
    async def test_evicted_session_is_re_registrable(self):
        """A Unity instance that comes back after being reaped must register cleanly."""
        registry = PluginRegistry()
        await _register(registry)
        _age_session(registry, "sess-1", 300)
        await registry.cleanup_stale(max_age_seconds=120)

        session, evicted = await registry.register(
            "sess-2", "MyProject", "hash1", "2022.3"
        )

        assert session.session_id == "sess-2"
        assert evicted is None, "a stale session's hash map entry was left behind"
        assert await registry.get_session_id_by_hash("hash1") == "sess-2"


class TestSeededStaleSession:
    """A pre-existing ghost (the shape a restart-free upgrade starts from) is reclaimed."""

    @pytest.mark.asyncio
    async def test_ghost_seeded_before_sweep_start_is_reclaimed(self):
        registry = PluginRegistry()
        await _register(registry, "ghost", project_hash="dead-hash")
        # No touch ever arrives for this one — exactly an orphaned crashed session.
        _age_session(registry, "ghost", 86_400)

        evicted = await registry.cleanup_stale(max_age_seconds=120)

        assert evicted == ["ghost"]
        assert await registry.list_sessions() == {}
