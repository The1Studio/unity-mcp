"""send_command liveness re-check (unity-mcp #108). Port of c83e9694."""

import asyncio

import pytest

from transport.plugin_hub import PluginHub, PluginDisconnectedError


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
