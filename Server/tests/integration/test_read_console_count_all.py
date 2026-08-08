import pytest
from .test_helpers import DummyContext, DummyMCP


def setup_console_tools():
    mcp = DummyMCP()
    import services.tools.read_console  # noqa: F401
    from services.registry import get_registered_tools
    for tool_info in get_registered_tools():
        if tool_info['name'] == 'read_console':
            mcp.tools[tool_info['name']] = tool_info['func']
    return mcp.tools


@pytest.mark.asyncio
@pytest.mark.parametrize("sentinel", ["all", "*", "ALL"])
async def test_read_console_count_all_sends_null(monkeypatch, sentinel):
    tools = setup_console_tools()
    read_console = tools["read_console"]
    captured = {}

    async def fake_send_with_unity_instance(_send_fn, _inst, _cmd, params, **_kw):
        captured["params"] = params
        return {"success": True, "data": {"lines": []}}

    import services.tools.read_console as mod
    monkeypatch.setattr(mod, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await read_console(ctx=DummyContext(), action="get", count=sentinel)
    assert resp["success"] is True
    assert captured["params"]["count"] is None, (
        f"count={sentinel!r} should mean 'all' (null), got {captured['params']['count']!r}"
    )
