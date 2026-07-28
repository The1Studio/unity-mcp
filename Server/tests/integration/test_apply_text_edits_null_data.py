"""Regression: apply_text_edits must not crash when the read-back returns data=null.

Bug: when an edit needs normalization (LSP range / index form), apply_text_edits
re-reads the file via `manage_script read`, then did
    data = read_resp.get("data", {}); data.get("contents")
A successful read can carry an explicit `"data": null`, so `.get("data", {})`
returned None and `None.get("contents")` raised an uncaught AttributeError (no
surrounding try/except). Same shape + fix as the find_in_file null-data guard.
"""

import pytest
from unittest.mock import patch

from .test_helpers import DummyContext, setup_script_tools


@pytest.mark.asyncio
async def test_apply_text_edits_survives_null_data_on_read():
    tools = setup_script_tools()
    apply = tools["apply_text_edits"]

    async def fake_send(cmd, params, **kwargs):
        if isinstance(params, dict) and params.get("action") == "read":
            return {"success": True, "data": None}  # success + explicit null data
        return {"success": True, "data": {}}

    with patch(
        "transport.legacy.unity_connection.async_send_command_with_retry", fake_send
    ):
        # A range-form edit forces normalization → triggers the read path.
        resp = await apply(
            DummyContext(), uri="Assets/X.cs", edits=[{"range": [0, 0], "text": "// x\n"}]
        )

    # Pre-fix: raised AttributeError. Post-fix: a normal dict response.
    assert isinstance(resp, dict)
