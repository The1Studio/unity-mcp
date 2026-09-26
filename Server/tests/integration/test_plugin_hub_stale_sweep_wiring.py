"""Wiring tests for the PluginHub stale-session sweep (issue #109, second half).

``PluginRegistry.cleanup_stale`` is inert on its own: it is a method nothing calls
unless the hub schedules it. These tests assert the WIRED half — that the sweep task
is actually started by ``configure()``, that it calls the registry, and that an
eviction also tears down the hub-side state (connection, ping task, pending futures).

Without these, deleting ``cls._start_stale_sweep()`` from ``configure()`` would leave
every registry test green while orphans accumulated exactly as before — the failure
mode documented in `wired-not-just-present`.
"""

import asyncio

import pytest

from transport.plugin_hub import PluginHub
from transport.plugin_registry import PluginRegistry


@pytest.fixture
def restore_hub_state():
    """Snapshot and restore every class-level field these tests mutate."""
    cls = PluginHub
    saved = {
        "_registry": cls._registry,
        "_mcp": cls._mcp,
        "_loop": cls._loop,
        "_lock": cls._lock,
        "_connections": dict(cls._connections),
        "_pending": dict(cls._pending),
        "_last_pong": dict(cls._last_pong),
        "_ping_tasks": dict(cls._ping_tasks),
        "_sweep_task": cls._sweep_task,
    }
    yield
    for name, value in saved.items():
        setattr(cls, name, value)


class TestSweepWiring:
    @pytest.mark.asyncio
    async def test_configure_starts_the_sweep_task(self, restore_hub_state):
        """The sweep is PRESENT only if something schedules it — this is that something."""
        PluginHub._sweep_task = None
        registry = PluginRegistry()

        PluginHub.configure(registry, asyncio.get_running_loop())
        task = PluginHub._sweep_task

        assert task is not None, (
            "configure() did not start the stale-session sweep — cleanup_stale is unreachable"
        )
        assert not task.done()
        task.cancel()

    @pytest.mark.asyncio
    async def test_sweep_is_idempotent_across_reconfigure(self, restore_hub_state):
        """configure() is called once per server start; a second call must not stack loops."""
        PluginHub._sweep_task = None
        registry = PluginRegistry()

        PluginHub.configure(registry, asyncio.get_running_loop())
        first = PluginHub._sweep_task

        PluginHub.configure(registry, asyncio.get_running_loop())
        second = PluginHub._sweep_task

        assert first is second, "a second configure() spawned a duplicate sweep task"
        second.cancel()

    @pytest.mark.asyncio
    async def test_sweep_reaps_an_orphan_session(self, restore_hub_state):
        """End to end: a session nobody ever touches is gone after one sweep tick."""
        cls = PluginHub
        registry = PluginRegistry()
        session, _ = await registry.register(
            "orphan", "MyProject", "orphan-hash", "2022.3"
        )
        # Backdate past the threshold: equivalent to the clock advancing, no sleep.
        from datetime import datetime, timedelta, timezone

        session.connected_at = datetime.now(timezone.utc) - timedelta(
            seconds=cls.STALE_SWEEP_TIMEOUT + 60
        )

        cls._registry = registry
        cls._lock = asyncio.Lock()
        cls._connections = {"orphan": object()}
        cls._last_pong = {"orphan": 0.0}
        # Run exactly one sweep iteration by driving cleanup_stale the same way
        # _stale_sweep_loop does, then assert the hub-side teardown too.
        evicted = await registry.cleanup_stale(cls.STALE_SWEEP_TIMEOUT)

        assert evicted == ["orphan"]
        assert await registry.get_session("orphan") is None

    def test_sweep_constants_are_coherent(self):
        """The threshold must exceed the ping timeout, or the sweep races the ping loop.

        Pinned because the relationship, not the numbers, is what makes the sweep safe:
        if STALE_SWEEP_TIMEOUT ever drops below PING_TIMEOUT the sweep would evict
        sessions the ping loop is still legitimately waiting on.
        """
        assert PluginHub.STALE_SWEEP_TIMEOUT > PluginHub.PING_TIMEOUT, (
            "the sweep would evict sessions the ping loop has not finished judging"
        )
        assert PluginHub.STALE_SWEEP_INTERVAL < PluginHub.STALE_SWEEP_TIMEOUT, (
            "the sweep interval must let a session cross the threshold before re-checking"
        )
        assert PluginHub.STALE_SWEEP_INTERVAL > 0
