import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock
import pytest
from services.tools.script_apply_edits import script_apply_edits

SOURCE = ("public class Player : MonoBehaviour\n{\n"
          "    public int health = 100;\n}\n")

@pytest.fixture
def unity(monkeypatch):
    captured = {}
    async def fake_read(tool_name, params, instance_id=None, **kwargs):
        assert params["action"] == "read"
        return {"success": True, "data": {"contents": SOURCE}}
    async def fake_send_mutation(ctx, unity_instance, tool_name, params, **kwargs):
        captured["params"] = params
        return {"success": True, "message": "ok"}
    monkeypatch.setattr("services.tools.script_apply_edits.get_unity_instance_from_context",
                        AsyncMock(return_value="unity-1"))
    monkeypatch.setattr("services.tools.script_apply_edits.async_send_command_with_retry", fake_read)
    monkeypatch.setattr("services.tools.script_apply_edits.send_mutation", fake_send_mutation)
    return captured

def _ctx():
    return SimpleNamespace(info=AsyncMock(), error=AsyncMock())

def test_regex_replace_honors_replacement_field(unity):
    asyncio.run(script_apply_edits(_ctx(), name="Player", path="Assets/Scripts",
        edits=[{"op": "regex_replace", "pattern": r"health = 100",
                "replacement": "health = 250"}]))
    spans = unity["params"]["edits"]
    assert len(spans) == 1
    assert spans[0]["newText"] == "health = 250", (
        f"regex_replace dropped the replacement text -> newText={spans[0]['newText']!r} "
        "(empty newText DELETES the matched code instead of replacing it)")

def test_regex_replace_honors_text_field_alias(unity):
    asyncio.run(script_apply_edits(_ctx(), name="Player", path="Assets/Scripts",
        edits=[{"op": "regex_replace", "pattern": r"health = 100",
                "text": "health = 250"}]))
    assert unity["params"]["edits"][0]["newText"] == "health = 250"
