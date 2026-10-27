"""Gate: screenshot capture must not leave Play mode paused (issue #131).

`ScreenshotUtility.CaptureCompositedAfterFrame` drives a `WaitForEndOfFrame` capture by
spinning the editor's player loop with `EditorApplication.Step()`. That call is the
editor's *Step* toolbar button: it advances one frame **and leaves the editor paused**.
Nothing restored the flag, so every `manage_camera action=screenshot` in Play mode froze
the game until something unpaused it. Agents then misread the freeze as a hung boot ("the
loading screen is stuck at 95%") and chased a game bug that was really a paused editor.

This gate exists because the behaviour cannot be covered by the Unity test runner here:
`.github/workflows/unity-tests.yml` skips every Unity test when the license secrets are
absent, and this repo configures none, so a PlayMode regression test would never run in
CI. The companion `ScreenshotPauseStateTests` ships alongside for anyone with a live
editor; this file is the half that actually gates a PR.

Scope: static shape only. It proves the pause state is saved and restored around the
`Step()` loop — it cannot prove Unity's runtime semantics, which is what the PlayMode test
is for.
"""

import re
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCREENSHOT_UTILITY = REPO_ROOT / "MCPForUnity" / "Runtime" / "Helpers" / "ScreenshotUtility.cs"
PLAYMODE_TEST = (
    REPO_ROOT
    / "TestProjects"
    / "UnityMCPTests"
    / "Assets"
    / "Tests"
    / "PlayMode"
    / "ScreenshotPauseStateTests.cs"
)

METHOD_NAME = "CaptureCompositedAfterFrame"


def _strip_comments_and_strings(source: str) -> str:
    """Drop comments and string literals so brace matching cannot be thrown off by them."""
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r"//[^\n]*", "", source)
    source = re.sub(r'@"(?:[^"]|"")*"', '""', source)
    source = re.sub(r'"(?:\\.|[^"\\])*"', '""', source)
    return re.sub(r"'(?:\\.|[^'\\])'", "''", source)


def _method_body(source: str, name: str) -> str:
    """Return the body of the method `name`, brace-matched from its opening brace."""
    match = re.search(rf"\b{name}\s*\(", source)
    assert match, f"{name} not found in {SCREENSHOT_UTILITY.name}"
    opening = source.find("{", match.end())
    assert opening != -1, f"{name} has no body"
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening : index + 1]
    pytest.fail(f"{name} body is not brace-balanced")


@pytest.fixture(scope="module")
def method_body() -> str:
    assert SCREENSHOT_UTILITY.is_file(), f"missing {SCREENSHOT_UTILITY}"
    return _method_body(
        _strip_comments_and_strings(SCREENSHOT_UTILITY.read_text(encoding="utf-8")),
        METHOD_NAME,
    )


def test_step_loop_is_present(method_body):
    """Guards the gate itself: if the capture stops calling Step, the check below is vacuous."""
    assert "EditorApplication.Step()" in method_body, (
        f"{METHOD_NAME} no longer calls EditorApplication.Step(); re-check whether issue #131 "
        "is still reachable before deleting this gate"
    )


def test_pause_state_is_read_before_the_step_loop(method_body):
    """The saved value must be captured before any Step() can mutate it."""
    read_at = method_body.find("EditorApplication.isPaused")
    step_at = method_body.find("EditorApplication.Step()")
    assert read_at != -1, (
        f"{METHOD_NAME} never reads EditorApplication.isPaused, so it cannot restore the "
        "caller's pause state (issue #131)"
    )
    assert read_at < step_at, (
        "the pause state must be read BEFORE the Step() loop, or the saved value is already "
        "the paused one the loop just set"
    )


def test_pause_state_is_restored_after_the_step_loop(method_body):
    """The restore must be an assignment, and must come after the loop."""
    assignments = list(
        re.finditer(r"EditorApplication\.isPaused\s*=\s*([A-Za-z_][A-Za-z0-9_]*)", method_body)
    )
    assert assignments, (
        f"{METHOD_NAME} never writes EditorApplication.isPaused, so a screenshot still leaves "
        "Play mode paused (issue #131)"
    )
    step_at = method_body.find("EditorApplication.Step()")
    assert any(
        match.start() > step_at for match in assignments
    ), "the pause-state restore must run after the Step() loop"
    assert any(
        match.group(1) not in {"false", "true"} for match in assignments
    ), (
        "the restore must assign the saved value, not a literal — forcing isPaused=false would "
        "resume a game the caller deliberately paused"
    )


def test_restore_runs_in_a_finally_block(method_body):
    """An exception mid-loop must not strand the editor paused."""
    restore_at = max(
        match.start()
        for match in re.finditer(r"EditorApplication\.isPaused\s*=", method_body)
    )
    finally_at = method_body.rfind("finally", 0, restore_at)
    assert finally_at != -1, (
        "the pause-state restore must sit in a finally block, otherwise a throw inside the "
        "capture leaves Play mode paused (issue #131)"
    )


def test_playmode_companion_test_ships():
    """The behavioural half. Skipped silently by CI today, but must exist and stay wired."""
    assert PLAYMODE_TEST.is_file(), (
        "ScreenshotPauseStateTests.cs is missing — this gate only checks source shape; the "
        "runtime assertion lives there"
    )
    source = PLAYMODE_TEST.read_text(encoding="utf-8")
    assert "CaptureComposited" in source, (
        "the PlayMode companion no longer exercises CaptureComposited"
    )
    assert PLAYMODE_TEST.with_suffix(".cs.meta").is_file(), (
        "a Unity asset needs a .meta file or the editor regenerates the GUID on first import"
    )
