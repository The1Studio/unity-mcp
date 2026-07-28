"""Regression: CLI commands must not crash when Unity returns an explicit
``"data": null`` alongside ``"success": true``.

``dict.get("data", {})`` only substitutes the ``{}`` default when the key is
ABSENT — a present-but-null ``data`` returns ``None``, so the following
``data.get(...)`` (or ``.get("data", {}).get(...)``) raised
``AttributeError: 'NoneType' object has no attribute 'get'``, uncaught, crashing
the command. The sibling async-job-id extraction in ``build.py`` / ``packages.py``
already guards this exact shape with ``(result.get("data") or {})``; these five
commands were the remaining unguarded copies.

Each test drives the real Click command through a mocked transport that returns
``{"success": True, "data": None}`` and asserts no ``AttributeError`` escapes.
Every assertion below fails on the pre-fix code.
"""

from unittest.mock import patch

from click.testing import CliRunner

from cli.commands.editor import editor
from cli.commands.scene import scene
from cli.commands.instance import instance
from cli.commands.shader import shader
from cli.utils.config import CLIConfig


CONFIG = CLIConfig(host="localhost", port=8080, format="text")
NULL_DATA = {"success": True, "data": None}


def _run(module, group, args, response):
    with patch(f"cli.commands.{module}.get_config", return_value=CONFIG), \
            patch(f"cli.commands.{module}.run_command", return_value=response):
        return CliRunner().invoke(group, args)


def test_editor_tests_async_null_data_no_crash():
    # `editor tests --async` → run_tests, line ~353.
    r = _run("editor", editor, ["tests", "--async"], NULL_DATA)
    assert not isinstance(r.exception, AttributeError), r.exception


def test_editor_poll_test_null_data_no_crash():
    # `editor poll-test <job>` → poll_test, line ~404.
    r = _run("editor", editor, ["poll-test", "job123"], NULL_DATA)
    assert not isinstance(r.exception, AttributeError), r.exception


def test_scene_validate_null_data_no_crash():
    # `scene validate` → validate, line ~342.
    r = _run("scene", scene, ["validate"], NULL_DATA)
    assert not isinstance(r.exception, AttributeError), r.exception


def test_instance_set_null_data_no_crash():
    # `instance set <id>` → set_instance, line ~70.
    r = _run("instance", instance, ["set", "unity-123"], NULL_DATA)
    assert not isinstance(r.exception, AttributeError), r.exception


def test_shader_read_null_data_no_crash():
    # `shader read <path>` → read_shader, line ~43.
    r = _run("shader", shader, ["read", "Assets/Shaders/Missing.shader"], NULL_DATA)
    assert not isinstance(r.exception, AttributeError), r.exception


def test_editor_tests_async_normal_path_still_works():
    # Fix must not disturb the normal (non-null) job-id path.
    resp = {"success": True, "data": {"job_id": "abc123"}}
    r = _run("editor", editor, ["tests", "--async"], resp)
    assert r.exit_code == 0
    assert "abc123" in r.output


def test_shader_read_normal_contents_still_work():
    resp = {"success": True, "data": {"contents": 'Shader "Custom/Test" {}'}}
    r = _run("shader", shader, ["read", "Assets/Shaders/Test.shader"], resp)
    assert r.exit_code == 0
    assert 'Shader "Custom/Test"' in r.output
