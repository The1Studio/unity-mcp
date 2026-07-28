"""Regression: `batch run` must not crash when the batch response has data=null.

Bug: batch_run did `result.get("data", {}).get("results", [])`, but .get(k, {})
only substitutes the default when the key is ABSENT — a failed/empty batch can
return an explicit `"data": null`, so the chain became `None.get("results")` and
raised an uncaught AttributeError. Sibling input_simulation.py guards this exact
shape with isinstance(result.get("data"), dict).
"""

import json
import os
import tempfile
from unittest.mock import patch

from click.testing import CliRunner

from cli.commands.batch import batch


def _run_with_response(resp):
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
        json.dump([{"tool": "manage_gameobject", "params": {"action": "create"}}], f)
        fn = f.name
    try:
        with patch("cli.commands.batch.run_command", return_value=resp):
            return CliRunner().invoke(batch, ["run", fn])
    finally:
        os.unlink(fn)


def test_batch_run_success_with_null_data_no_crash():
    # A failed batch commonly returns data=null. Pre-fix: AttributeError.
    r = _run_with_response({"success": False, "message": "batch failed", "data": None})
    assert not isinstance(r.exception, AttributeError)


def test_batch_run_normal_results_still_summarized():
    r = _run_with_response(
        {"success": True, "data": {"results": [{"success": True}, {"success": False}]}}
    )
    assert not isinstance(r.exception, AttributeError)
    assert "1 succeeded, 1 failed" in r.output


def test_batch_run_missing_data_key_no_crash():
    r = _run_with_response({"success": True})  # no data key at all
    assert not isinstance(r.exception, AttributeError)
