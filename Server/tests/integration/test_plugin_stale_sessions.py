"""Stale-session TTL expiry and send_command liveness (unity-mcp #108, #109)."""

import asyncio
from datetime import datetime, timedelta, timezone

import pytest

from transport.plugin_hub import PluginHub, PluginDisconnectedError
from transport.plugin_registry import PluginRegistry


class TestRegistryTtl:
    @pytest.mark.asyncio
    async def test_cleanup_stale_evicts_only_old_sessions(self):
        registry = PluginRegistry()
        await registry.register("old", "P", "h-old", "2022")
        await registry.register("new", "P", "h-new", "2022")
        (await registry.get_session("old")).connected_at = datetime.now(
            timezone.utc) - timedelta(seconds=500)

        evicted = await registry.cleanup_stale(60)

        assert evicted == ["old"]
        assert await registry.get_session("old") is None
        assert await registry.get_session_id_by_hash("h-old") is None
        assert await registry.get_session("new") is not None

    @pytest.mark.asyncio
    async def test_touch_keeps_session_alive(self):
        registry = PluginRegistry()
        await registry.register("s", "P", "h", "2022")
        (await registry.get_session("s")).connected_at = datetime.now(
            timezone.utc) - timedelta(seconds=500)
        await registry.touch("s")
        assert await registry.cleanup_stale(60) == []


class TestSendCommandLiveness:
    @pytest.mark.asyncio
    async def test_send_command_fails_fast_when_connection_removed_after_lookup(self, monkeypatch):
        class FakeWs:
            async def send_json(self, _):
                raise AssertionError("must not send on a disconnected session")

        ws = FakeWs()
        monkeypatch.setattr(PluginHub, "_lock", asyncio.Lock())
        monkeypatch.setattr(PluginHub, "_connections", {})
        monkeypatch.setattr(PluginHub, "_pending", {})

        async def fake_get_connection(_sid):
            return ws  # session already gone from _connections by lock time

        monkeypatch.setattr(PluginHub, "_get_connection", fake_get_connection)
        with pytest.raises(PluginDisconnectedError):
            await PluginHub.send_command("gone", "ping", {})
        assert PluginHub._pending == {}
