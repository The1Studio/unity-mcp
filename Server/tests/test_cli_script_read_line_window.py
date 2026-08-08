"""Regression: `script read` / `code read` must honour --start-line / --line-count.

Both commands forward the window as startLine/lineCount to manage_script, but
Unity's ManageScript.cs dispatches `case "read"` to ReadScript(fullPath,
relativePath) — a two-argument helper that reads the whole file and never
consults those keys. The CLI then echoed data["contents"] verbatim, so both
documented flags were silently dropped and the user got the entire file. Fix:
apply the 1-based line window client-side (like `code search` does its match
pass locally).
"""

from unittest.mock import patch

import pytest
from click.testing import CliRunner

from cli.commands.code import code
from cli.commands.script import script
from cli.utils.config import CLIConfig

FILE_BODY = "\n".join(f"L{i}" for i in range(1, 11))


@pytest.fixture
def runner():
    return CliRunner()


@pytest.fixture
def mock_config():
    return CLIConfig(host="127.0.0.1", port=8080, format="text")


def _read_response():
    return {
        "success": True,
        "data": {
            "uri": "mcpforunity://path/Assets/Scripts/A.cs",
            "path": "Assets/Scripts/A.cs",
            "contents": FILE_BODY,
            "contentsEncoded": False,
        },
    }


@pytest.mark.parametrize("group, module", [(script, "cli.commands.script"), (code, "cli.commands.code")])
def test_read_applies_start_line_and_line_count(runner, mock_config, group, module):
    """--start-line 3 --line-count 2 must print exactly L3 and L4."""
    with patch(f"{module}.get_config", return_value=mock_config):
        with patch(f"{module}.run_command", return_value=_read_response()):
            result = runner.invoke(
                group,
                ["read", "Assets/Scripts/A.cs", "--start-line", "3", "--line-count", "2"],
                catch_exceptions=False,
            )

    assert result.exit_code == 0
    assert result.output == "L3\nL4\n"


@pytest.mark.parametrize("group, module", [(script, "cli.commands.script"), (code, "cli.commands.code")])
def test_read_start_line_only_reads_to_end(runner, mock_config, group, module):
    with patch(f"{module}.get_config", return_value=mock_config):
        with patch(f"{module}.run_command", return_value=_read_response()):
            result = runner.invoke(
                group,
                ["read", "Assets/Scripts/A.cs", "--start-line", "9"],
                catch_exceptions=False,
            )

    assert result.exit_code == 0
    assert result.output == "L9\nL10\n"


@pytest.mark.parametrize("group, module", [(script, "cli.commands.script"), (code, "cli.commands.code")])
def test_read_without_window_is_unchanged(runner, mock_config, group, module):
    """No window flags => byte-identical passthrough (no behaviour change)."""
    with patch(f"{module}.get_config", return_value=mock_config):
        with patch(f"{module}.run_command", return_value=_read_response()):
            result = runner.invoke(
                group, ["read", "Assets/Scripts/A.cs"], catch_exceptions=False
            )

    assert result.exit_code == 0
    assert result.output == FILE_BODY + "\n"
