"""Regression tests for message-only success envelopes in CLI text output.

Unity's `SuccessResponse` declares `data` with `NullValueHandling.Ignore`
(MCPForUnity/Editor/Helpers/Response.cs), and many handlers return a bare
`{success, message}` object (e.g. LightBakingOps.CancelBake). `format_as_text`
skips the meta fields `success`/`error`/`message`, so those responses used to
render as an empty string and the CLI printed a blank line.
"""

from cli.utils.output import format_as_text, format_output


def test_message_only_success_response_prints_message():
    """`graphics bake-cancel` shape: success + message, no data key."""
    out = format_as_text({"success": True, "message": "Light bake cancelled."})
    assert "Light bake cancelled." in out


def test_message_only_success_response_via_format_output():
    out = format_output({"success": True, "message": "Bake data cleared."}, "text")
    assert out.strip() != ""
    assert "Bake data cleared." in out


def test_success_with_payload_still_renders_payload_only():
    """Unchanged behavior: a real payload wins over the message."""
    out = format_as_text({"success": True, "message": "ok", "data": {"a": 1}})
    assert out == "a: 1"


def test_error_response_unchanged():
    out = format_as_text({"success": False, "message": "Boom"})
    assert out == "❌ Error: Boom"


def test_extra_top_level_keys_are_not_replaced_by_message():
    """Guard: the message fallback must not swallow real top-level content."""
    out = format_as_text({"success": True, "message": "ok", "count": 3})
    assert "count: 3" in out
