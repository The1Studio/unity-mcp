import pytest

from .test_helpers import DummyContext
import services.tools.manage_ui as ui_mod


@pytest.mark.asyncio
async def test_manage_ui_read_success_with_null_data(monkeypatch):
    """A successful ``read`` response carrying an explicit ``data: null`` must be
    returned cleanly, not crash with ``AttributeError: 'NoneType' object has no
    attribute 'get'``.

    Regression: ``result.get("data", {})`` returns ``None`` (not ``{}``) when the
    key is present but null — ``.get(key, default)`` only substitutes the default
    when the key is ABSENT. The following ``data.get("contentsEncoded")`` then
    crashed, and that line sits OUTSIDE the try/except around the base64 decode, so
    the exception escaped the tool uncaught. Fixed with ``or {}`` to match the
    sibling fixes in find_in_file / manage_script.apply_text_edits."""

    async def fake_instance(ctx):
        return None

    async def fake_send(fn, unity_instance, command, params, **kwargs):
        return {"success": True, "data": None}

    monkeypatch.setattr(ui_mod, "get_unity_instance_from_context", fake_instance)
    monkeypatch.setattr(ui_mod, "send_with_unity_instance", fake_send)

    resp = await ui_mod.manage_ui(
        ctx=DummyContext(),
        action="read",
        path="Assets/UI/MainMenu.uxml",
    )

    # No crash — the success response is returned unchanged.
    assert isinstance(resp, dict)
    assert resp.get("success") is True
    assert resp.get("data") is None
