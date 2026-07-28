"""Regression: camera CLI commands must call run_command with the correct
positional argument order.

Bug: every command in cli/commands/camera.py called
    run_command(config, "manage_camera", params)
but run_command's signature is run_command(command_type, params, config=None).
The rotated args put the CLIConfig in the command_type slot and the params dict
in the config slot, so send_command bound cfg=<params dict> and crashed on
`cfg.host` (AttributeError) before its try-block — the whole `camera` command
group was 100% non-functional. Commands also called format_output(result, config)
(CLIConfig where a format string is expected, with no click.echo), so nothing
printed. Every other command file uses run_command("manage_x", params, config)
and click.echo(format_output(result, config.format)).
"""

from unittest.mock import patch
import pytest
from click.testing import CliRunner

from cli.commands.camera import camera
from cli.utils.config import CLIConfig


@pytest.fixture
def mock_config():
    return CLIConfig(host="localhost", port=8080, format="text")


@pytest.fixture
def runner():
    return CliRunner()


@pytest.mark.parametrize(
    "argv, expected_action",
    [
        (["ping"], "ping"),
        (["list"], "list_cameras"),
        (["brain-status"], "get_brain_status"),
    ],
)
def test_camera_run_command_arg_order(runner, mock_config, argv, expected_action):
    resp = {"success": True, "data": {}}
    with patch("cli.commands.camera.get_config", return_value=mock_config):
        with patch("cli.commands.camera.run_command", return_value=resp) as mock_run:
            result = runner.invoke(camera, argv, catch_exceptions=False)

    assert result.exit_code == 0
    mock_run.assert_called_once()
    args = mock_run.call_args.args
    # Correct order: (command_type, params, config)
    assert args[0] == "manage_camera"          # pre-fix: this was the CLIConfig object
    assert isinstance(args[1], dict)           # pre-fix: this was the string "manage_camera"
    assert args[1]["action"] == expected_action
    assert args[2] is mock_config              # config passed third


def test_camera_ping_emits_output(runner, mock_config):
    """format_output result must be echoed (pre-fix: no click.echo → empty)."""
    resp = {"success": True, "data": {"pong": True}}
    with patch("cli.commands.camera.get_config", return_value=mock_config):
        with patch("cli.commands.camera.run_command", return_value=resp):
            result = runner.invoke(camera, ["ping"], catch_exceptions=False)
    assert result.exit_code == 0
    assert result.output.strip() != ""         # pre-fix: nothing printed
