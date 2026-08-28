"""
Tests for the manage_profiler tool.
Validates parameter routing for Unity Profiler operations.
"""
import pytest

from .test_helpers import DummyContext
import services.tools.manage_profiler as profiler_mod


def _fake_send_factory(captured: dict, response: dict = None):
    if response is None:
        response = {"success": True, "message": "OK", "data": {}}

    async def fake_send(cmd, params, **kwargs):
        captured["cmd"] = cmd
        captured["params"] = params
        return response

    return fake_send


@pytest.mark.asyncio
async def test_get_counters(monkeypatch):
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    resp = await profiler_mod.manage_profiler(
        ctx=DummyContext(), action="get_counters", counters="Main Thread,Render Thread",
    )
    assert resp["success"] is True
    assert captured["params"]["counters"] == "Main Thread,Render Thread"


@pytest.mark.asyncio
async def test_profiler_status(monkeypatch):
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    resp = await profiler_mod.manage_profiler(ctx=DummyContext(), action="profiler_status")
    assert resp["success"] is True
    assert captured["cmd"] == "manage_profiler"
    assert captured["params"]["action"] == "profiler_status"


@pytest.mark.asyncio
async def test_profiler_start_recording(monkeypatch):
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    resp = await profiler_mod.manage_profiler(
        ctx=DummyContext(), action="profiler_start",
        log_file="/tmp/profile.raw", enable_callstacks=True,
    )
    assert resp["success"] is True
    assert captured["params"]["log_file"] == "/tmp/profile.raw"
    assert captured["params"]["enable_callstacks"] is True


@pytest.mark.asyncio
async def test_frame_debugger_get_events_pagination(monkeypatch):
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    resp = await profiler_mod.manage_profiler(
        ctx=DummyContext(), action="frame_debugger_get_events", page_size=20, cursor=40,
    )
    assert resp["success"] is True
    assert captured["params"]["page_size"] == 20
    assert captured["params"]["cursor"] == 40


@pytest.mark.asyncio
async def test_unknown_action_rejected(monkeypatch):
    """An action outside ALL_ACTIONS is rejected before the transport is touched."""
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    resp = await profiler_mod.manage_profiler(ctx=DummyContext(), action="list_categories")
    assert resp["success"] is False
    assert "Unknown action 'list_categories'" in resp["message"]
    assert "params" not in captured


@pytest.mark.asyncio
async def test_none_params_stripped(monkeypatch):
    captured = {}
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", _fake_send_factory(captured))
    await profiler_mod.manage_profiler(ctx=DummyContext(), action="ping")
    assert set(captured["params"].keys()) == {"action"}


@pytest.mark.asyncio
async def test_python_exception_caught(monkeypatch):
    async def raising_send(cmd, params, **kwargs):
        raise ConnectionError("Unity not connected")
    monkeypatch.setattr(profiler_mod, "async_send_command_with_retry", raising_send)
    resp = await profiler_mod.manage_profiler(ctx=DummyContext(), action="ping")
    assert resp["success"] is False
    assert "Unity not connected" in resp["message"]
