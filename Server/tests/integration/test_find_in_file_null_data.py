import pytest

from .test_helpers import DummyContext
import services.tools.find_in_file as find_mod


@pytest.mark.asyncio
async def test_find_in_file_success_with_null_data(monkeypatch):
    """A success response carrying an explicit ``data: null`` must return a clean
    error, not crash with ``AttributeError: 'NoneType' object has no attribute 'get'``.

    Regression: ``read_resp.get("data", {})`` returns ``None`` (not ``{}``) when the
    key is present but null, so the following ``data.get("contents")`` crashed. The
    codebase itself produces such responses (e.g. a tool returning
    ``data=response.get("data")`` where ``data`` is absent upstream)."""

    async def fake_send(cmd, params, **kwargs):
        return {"success": True, "data": None}

    monkeypatch.setattr(find_mod, "async_send_command_with_retry", fake_send)

    resp = await find_mod.find_in_file(
        ctx=DummyContext(),
        uri="Assets/Scripts/Foo.cs",
        pattern="class",
    )

    # No crash — a clean, structured failure instead.
    assert isinstance(resp, dict)
    assert resp.get("success") is False
    assert "Could not read file content" in resp.get("message", "")
