"""
Tests for the manage_input_simulation tool.
Validates parameter routing for Play Mode input simulation actions.
"""
import pytest

from .test_helpers import DummyContext
import services.tools.manage_input_simulation as sim_mod


def _fake_send_factory(captured: dict, response: dict = None):
    if response is None:
        response = {"success": True, "message": "OK", "data": {}}

    async def fake_send(cmd, params, **kwargs):
        captured["cmd"] = cmd
        captured["params"] = params
        return response

    return fake_send


# ---------------------------------------------------------------------------
# Action routing tests — one per action
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_discover(monkeypatch):
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Found 3 elements.", "data": {"elements": []}}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="discover",
        filter="Button",
        page_size=10,
    )
    assert resp["success"] is True
    assert captured["cmd"] == "manage_input_simulation"
    assert captured["params"]["action"] == "discover"
    assert captured["params"]["filter"] == "Button"
    assert captured["params"]["page_size"] == 10


@pytest.mark.asyncio
async def test_get_element_bounds(monkeypatch):
    captured = {}
    target = {"mode": "ui_element", "path": "Canvas/Button"}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Bounds retrieved.", "data": {"rect": {}}}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="get_element_bounds",
        target=target,
    )
    assert resp["success"] is True
    assert captured["cmd"] == "manage_input_simulation"
    assert captured["params"]["action"] == "get_element_bounds"
    assert captured["params"]["target"] == target


@pytest.mark.asyncio
async def test_click(monkeypatch):
    captured = {}
    target = {"mode": "coordinates", "x": 500, "y": 300}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Clicked."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="click",
        target=target,
        button="left",
    )
    assert resp["success"] is True
    assert captured["cmd"] == "manage_input_simulation"
    assert captured["params"]["action"] == "click"
    assert captured["params"]["target"] == target
    assert captured["params"]["button"] == "left"


@pytest.mark.asyncio
async def test_double_click(monkeypatch):
    captured = {}
    target = {"mode": "coordinates", "x": 200, "y": 150}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Double-clicked."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="double_click",
        target=target,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "double_click"
    assert captured["params"]["target"] == target


@pytest.mark.asyncio
async def test_mouse_move(monkeypatch):
    captured = {}
    target = {"mode": "coordinates", "x": 100, "y": 200}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="mouse_move",
        target=target,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "mouse_move"
    assert captured["params"]["target"] == target


@pytest.mark.asyncio
async def test_drag(monkeypatch):
    captured = {}
    from_target = {"mode": "coordinates", "x": 100, "y": 100}
    to_target = {"mode": "coordinates", "x": 400, "y": 400}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Dragged."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="drag",
        from_target=from_target,
        to_target=to_target,
        duration_ms=500,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "drag"
    assert captured["params"]["from_target"] == from_target
    assert captured["params"]["to_target"] == to_target
    assert captured["params"]["duration_ms"] == 500


@pytest.mark.asyncio
async def test_scroll(monkeypatch):
    captured = {}
    target = {"mode": "coordinates", "x": 300, "y": 300}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Scrolled."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="scroll",
        target=target,
        delta_x=0.0,
        delta_y=-3.0,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "scroll"
    assert captured["params"]["target"] == target
    assert captured["params"]["delta_x"] == 0.0
    assert captured["params"]["delta_y"] == -3.0


@pytest.mark.asyncio
async def test_key_press(monkeypatch):
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Key pressed."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="key_press",
        key="space",
        mode="tap",
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "key_press"
    assert captured["params"]["key"] == "space"
    assert captured["params"]["mode"] == "tap"


@pytest.mark.asyncio
async def test_key_combo(monkeypatch):
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Key combo executed."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="key_combo",
        modifiers=["ctrl"],
        key="s",
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "key_combo"
    assert captured["params"]["modifiers"] == ["ctrl"]
    assert captured["params"]["key"] == "s"


@pytest.mark.asyncio
async def test_text_input(monkeypatch):
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Text typed."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="text_input",
        text="hello",
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "text_input"
    assert captured["params"]["text"] == "hello"


@pytest.mark.asyncio
async def test_touch_tap(monkeypatch):
    captured = {}
    target = {"mode": "coordinates", "x": 250, "y": 400}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Touch tapped."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="touch_tap",
        target=target,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "touch_tap"
    assert captured["params"]["target"] == target


@pytest.mark.asyncio
async def test_touch_swipe(monkeypatch):
    captured = {}
    from_target = {"mode": "coordinates", "x": 100, "y": 500}
    to_target = {"mode": "coordinates", "x": 100, "y": 200}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Touch swiped."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="touch_swipe",
        from_target=from_target,
        to_target=to_target,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "touch_swipe"
    assert captured["params"]["from_target"] == from_target
    assert captured["params"]["to_target"] == to_target


@pytest.mark.asyncio
async def test_touch_pinch(monkeypatch):
    captured = {}
    center_target = {"mode": "coordinates", "x": 400, "y": 300}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Pinch executed."}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="touch_pinch",
        center_target=center_target,
        start_distance=50.0,
        end_distance=150.0,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "touch_pinch"
    assert captured["params"]["center_target"] == center_target
    assert captured["params"]["start_distance"] == 50.0
    assert captured["params"]["end_distance"] == 150.0


@pytest.mark.asyncio
async def test_sequence(monkeypatch):
    captured = {}
    steps = [
        {"action": "click", "params": {"target": {"mode": "coordinates", "x": 100, "y": 100}}, "delay_ms": 200},
        {"action": "key_press", "params": {"key": "escape"}, "delay_ms": 0},
    ]
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Sequence started.", "data": {"job_id": "abc123"}}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="sequence",
        steps=steps,
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "sequence"
    assert captured["params"]["steps"] == steps


@pytest.mark.asyncio
async def test_status(monkeypatch):
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured, {"success": True, "message": "Job complete.", "data": {"status": "done"}}),
    )
    resp = await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="status",
        job_id="abc123",
    )
    assert resp["success"] is True
    assert captured["params"]["action"] == "status"
    assert captured["params"]["job_id"] == "abc123"


# ---------------------------------------------------------------------------
# Edge case tests
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_none_params_stripped(monkeypatch):
    """None values must not appear in the params sent to Unity."""
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured),
    )
    await sim_mod.manage_input_simulation(ctx=DummyContext(), action="discover")
    assert set(captured["params"].keys()) == {"action"}


@pytest.mark.asyncio
async def test_python_exception_propagates(monkeypatch):
    """Exceptions from the transport layer propagate out of the tool (no suppression)."""
    async def raising_send(cmd, params, **kwargs):
        raise ConnectionError("Unity not connected")

    monkeypatch.setattr(sim_mod, "async_send_command_with_retry", raising_send)
    with pytest.raises(ConnectionError, match="Unity not connected"):
        await sim_mod.manage_input_simulation(ctx=DummyContext(), action="discover")


@pytest.mark.asyncio
async def test_target_dict_passthrough(monkeypatch):
    """Complex nested target dicts pass through to Unity unchanged."""
    captured = {}
    complex_target = {
        "mode": "gameobject",
        "path": "Level/Player/Body",
        "component": "Button",
        "offset_x": 10,
        "offset_y": -5,
    }
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured),
    )
    await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="click",
        target=complex_target,
    )
    assert captured["params"]["target"] == complex_target


@pytest.mark.asyncio
async def test_flush_param(monkeypatch):
    """flush=True must be forwarded in params."""
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured),
    )
    await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="click",
        target={"mode": "coordinates", "x": 0, "y": 0},
        flush=True,
    )
    assert captured["params"]["flush"] is True


@pytest.mark.asyncio
async def test_capture_after_param(monkeypatch):
    """capture_after=True must be forwarded in params."""
    captured = {}
    monkeypatch.setattr(
        sim_mod, "async_send_command_with_retry",
        _fake_send_factory(captured),
    )
    await sim_mod.manage_input_simulation(
        ctx=DummyContext(),
        action="click",
        target={"mode": "coordinates", "x": 0, "y": 0},
        capture_after=True,
    )
    assert captured["params"]["capture_after"] is True
