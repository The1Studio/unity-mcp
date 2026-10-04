"""Unit tests for the shared-daemon selection helpers (the end-to-end proof is
``test_shared_daemon_multi_editor.py``)."""
from unittest.mock import AsyncMock, Mock

import pytest

from transport.plugin_hub import InstanceSelectionRequiredError, PluginHub
from transport.unity_instance_middleware import (
    UnityInstanceMiddleware,
    add_unity_instance_param,
)


def test_add_param_is_optional_idempotent_and_non_mutating():
    schema = {
        "type": "object",
        "properties": {"action": {"type": "string"}},
        "required": ["action"],
        "additionalProperties": False,
    }
    out = add_unity_instance_param(schema)
    assert "unity_instance" in out["properties"]
    assert out["required"] == ["action"]
    assert out["additionalProperties"] is False
    assert "unity_instance" not in schema["properties"]  # input untouched
    assert add_unity_instance_param(out) is out  # idempotent


@pytest.mark.parametrize("bad", [None, {}, {"properties": None}, "x"])
def test_add_param_ignores_non_object_schemas(bad):
    assert add_unity_instance_param(bad) is bad


def _ctx(session_id=None, client_id=None):
    ctx = Mock()
    ctx.client_id = client_id
    ctx.session_id = session_id
    ctx.get_state = AsyncMock(return_value=None)
    return ctx


@pytest.mark.asyncio
async def test_sessions_without_client_id_do_not_share_a_selection():
    mw = UnityInstanceMiddleware()
    c1, c2 = _ctx("sess-1"), _ctx("sess-2")
    await mw.set_active_instance(c1, "A@aaa")
    await mw.set_active_instance(c2, "B@bbb")
    assert await mw.get_active_instance(c1) == "A@aaa"
    assert await mw.get_active_instance(c2) == "B@bbb"


@pytest.mark.asyncio
async def test_no_session_at_all_still_uses_the_global_key():
    mw = UnityInstanceMiddleware()
    assert await mw.get_session_key(_ctx()) == "global"


@pytest.mark.asyncio
async def test_session_id_raising_outside_a_request_falls_back_to_global():
    class Ctx:
        client_id = None
        get_state = AsyncMock(return_value=None)

        @property
        def session_id(self):
            raise RuntimeError("no session")

    assert await UnityInstanceMiddleware().get_session_key(Ctx()) == "global"


@pytest.mark.asyncio
async def test_selection_table_is_bounded_and_keeps_recent_entries():
    mw = UnityInstanceMiddleware()
    mw._MAX_TRACKED_SESSIONS = 3
    for i in range(5):
        await mw.set_active_instance(_ctx(f"s{i}"), f"P{i}@h{i}")
    assert len(mw._active_by_key) == 3
    assert await mw.get_active_instance(_ctx("s0")) is None
    assert await mw.get_active_instance(_ctx("s4")) == "P4@h4"


def test_selection_error_names_the_candidates():
    with pytest.raises(InstanceSelectionRequiredError) as exc:
        PluginHub._raise_if_ambiguous_selection(
            None, 2, False, None, available=["B@bbb", "A@aaa"])
    msg = str(exc.value)
    assert "A@aaa, B@bbb" in msg and "unity_instance" in msg


@pytest.mark.parametrize("uri,exempt", [
    ("mcpforunity://instances", True),
    ("mcpforunity://instances/", False),
    ("mcpforunity://instances?x=1", False),
    ("mcpforunity://instances-evil", False),
    ("mcpforunity://editor/state", False),
    ("xmcpforunity://instances", False),
])
def test_only_the_exact_instances_uri_is_exempt(uri, exempt):
    ctx = Mock()
    ctx.message = Mock(uri=uri)
    assert UnityInstanceMiddleware._is_instances_resource_read(ctx) is exempt


def test_shipped_session_cap():
    assert UnityInstanceMiddleware._MAX_TRACKED_SESSIONS == 1024
