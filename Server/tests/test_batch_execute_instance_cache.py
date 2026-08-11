"""Repro: batch_execute's max-commands cache was a module-global scalar, not
keyed per Unity instance.

The limit is a Unity-side EditorPrefs value read through an instance-routed
get_editor_state query, so a single shared server process could apply whichever
instance's limit was read FIRST to every instance thereafter — wrongly rejecting
a valid batch on an instance configured with a higher limit.
"""
import pytest

from models.models import MCPResponse
from tests.integration.test_helpers import DummyContext
import services.resources.editor_state as editor_state_module
import services.tools.batch_execute as batch_execute_module
from services.tools.batch_execute import invalidate_cached_max_commands


LIMITS = {
    "ProjectA@aaaaaa": 25,
    "ProjectB@bbbbbb": 100,
}


@pytest.fixture(autouse=True)
def _reset_cache():
    invalidate_cached_max_commands()
    yield
    invalidate_cached_max_commands()


@pytest.fixture
def fake_editor_state(monkeypatch):
    async def _fake_get_editor_state(ctx):
        instance = await ctx.get_state("unity_instance")
        return MCPResponse(
            success=True,
            message="ok",
            data={"settings": {"batch_execute_max_commands": LIMITS[instance]}},
        )

    monkeypatch.setattr(editor_state_module, "get_editor_state", _fake_get_editor_state)


def _ctx(instance: str) -> DummyContext:
    ctx = DummyContext()
    ctx._state["unity_instance"] = instance
    return ctx


@pytest.mark.asyncio
async def test_limit_is_resolved_per_unity_instance(fake_editor_state):
    """Instance B must see its own limit, not the one cached for instance A."""
    ctx_a = _ctx("ProjectA@aaaaaa")
    ctx_b = _ctx("ProjectB@bbbbbb")

    assert await batch_execute_module._get_max_commands_from_editor_state(ctx_a) == 25
    assert await batch_execute_module._get_max_commands_from_editor_state(ctx_b) == 100
    # The first instance keeps its own value afterwards.
    assert await batch_execute_module._get_max_commands_from_editor_state(ctx_a) == 25


@pytest.mark.asyncio
async def test_batch_not_rejected_using_another_instances_limit(fake_editor_state, monkeypatch):
    """A 50-command batch is valid on ProjectB (limit 100) even after ProjectA ran."""
    sent: list[dict] = []

    async def _fake_send(send_fn, unity_instance, command_type, payload, **kwargs):
        sent.append({"instance": unity_instance, "payload": payload, "kwargs": kwargs})
        return {"success": True, "data": {}}

    monkeypatch.setattr(batch_execute_module, "send_with_unity_instance", _fake_send)

    one_command = [{"tool": "manage_editor", "params": {"action": "get_state"}}]

    # Instance A (limit 25) runs first and populates the cache.
    await batch_execute_module.batch_execute(_ctx("ProjectA@aaaaaa"), one_command)
    # Instance B allows 100 commands; 50 must be accepted, not rejected.
    await batch_execute_module.batch_execute(_ctx("ProjectB@bbbbbb"), one_command * 50)

    assert len(sent) == 2
    assert len(sent[1]["payload"]["commands"]) == 50


@pytest.mark.asyncio
async def test_batch_opts_out_of_connection_level_retry(fake_editor_state, monkeypatch):
    """batch_execute must send retry_on_reload=False (issue #18).

    The batch applies non-idempotent mutations sequentially with no completion
    registry, so a connection-level replay re-runs every command that already
    succeeded — a 4-command batch produced 16 nodes. The kwarg is the whole fix,
    so assert it explicitly rather than trusting the call site.
    """
    sent: list[dict] = []

    async def _fake_send(send_fn, unity_instance, command_type, payload, **kwargs):
        sent.append({"command_type": command_type, "kwargs": kwargs})
        return {"success": True, "data": {}}

    monkeypatch.setattr(batch_execute_module, "send_with_unity_instance", _fake_send)

    await batch_execute_module.batch_execute(
        _ctx("ProjectA@aaaaaa"),
        [{"tool": "manage_editor", "params": {"action": "get_state"}}],
    )

    assert len(sent) == 1
    assert sent[0]["command_type"] == "batch_execute"
    # Explicitly False, not merely absent: absent means the default (True) applies.
    assert sent[0]["kwargs"].get("retry_on_reload") is False
