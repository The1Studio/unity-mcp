"""Regression tests for CLI HTTP route Unity-instance auto-selection (#66).

With more than one Unity Editor connected, `/api/command` (the no-explicit-
`unity_instance` path) and `/api/custom-tools` used to fall back to "pick the
first session in the dict" -- dict insertion order, i.e. whichever Unity
Editor happened to register first. That silently routes the call to the
wrong project: the request still succeeds, it's just answered by a different
Unity instance than the caller is working in.

Both routes now share `main._select_default_session_id`, which prefers the
sole connected session whose `project_path` contains the server process's
cwd. These tests exercise that helper directly (the routes are thin wrappers
around it -- see `src/main.py`), registering the "wrong" project first so a
naive first-available fallback would deterministically pick it, proving the
selection is cwd-driven rather than accidental.

NOTE: `main` is imported lazily inside each test (not at module scope). It
pulls in the REAL `core.telemetry` module as an import side effect, which
initializes the telemetry singleton before `tests/test_core_infrastructure_
characterization.py`'s fixtures get a chance to mock it -- pytest imports all
test modules up front during collection, so a module-level import here would
run before any test executes, regardless of file/run order. Importing inside
each test function defers that side effect until this file's own tests
actually run (mirrors the existing pattern in test_instance_autoselect.py).
"""
from __future__ import annotations

from transport.models import SessionDetails


def _sessions(*, wrong_path: str, right_path: str) -> dict[str, SessionDetails]:
    # Insertion order matters: "session-wrong" is inserted FIRST, so a plain
    # `next(iter(...))` fallback would pick it -- exactly the pre-fix bug.
    return {
        "session-wrong": SessionDetails(
            project="ProjectA",
            hash="aaaaaaaa",
            unity_version="6000.0",
            connected_at="now",
            project_path=wrong_path,
        ),
        "session-right": SessionDetails(
            project="ProjectB",
            hash="bbbbbbbb",
            unity_version="6000.0",
            connected_at="now",
            project_path=right_path,
        ),
    }


def test_selects_session_whose_project_path_contains_cwd(tmp_path, monkeypatch):
    import main

    project_a = tmp_path / "ProjectA"
    project_b = tmp_path / "ProjectB"
    project_a.mkdir()
    project_b.mkdir()
    monkeypatch.chdir(project_b)

    sessions = _sessions(wrong_path=str(project_a), right_path=str(project_b))

    assert main._select_default_session_id(sessions) == "session-right"


def test_selects_session_when_cwd_is_nested_under_project_assets_folder(tmp_path, monkeypatch):
    """project_path may point at the project root while cwd is deep inside it."""
    import main

    project_a = tmp_path / "ProjectA"
    project_b = tmp_path / "ProjectB"
    (project_b / "Assets" / "Scripts").mkdir(parents=True)
    project_a.mkdir()
    monkeypatch.chdir(project_b / "Assets" / "Scripts")

    sessions = _sessions(wrong_path=str(project_a), right_path=str(project_b))

    assert main._select_default_session_id(sessions) == "session-right"


def test_falls_back_to_first_available_when_no_session_matches_cwd(tmp_path, monkeypatch):
    """Preserves the old behaviour when cwd doesn't disambiguate anything."""
    import main

    project_a = tmp_path / "ProjectA"
    project_b = tmp_path / "ProjectB"
    project_a.mkdir()
    project_b.mkdir()
    elsewhere = tmp_path / "elsewhere"
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    sessions = _sessions(wrong_path=str(project_a), right_path=str(project_b))

    assert main._select_default_session_id(sessions) == "session-wrong"


def test_falls_back_to_first_available_when_multiple_sessions_match_cwd(tmp_path, monkeypatch):
    """Ambiguous cwd matches must not guess -- keep the prior first-available pick."""
    import main

    shared_root = tmp_path / "shared"
    shared_root.mkdir()
    monkeypatch.chdir(shared_root)

    sessions = _sessions(wrong_path=str(shared_root), right_path=str(shared_root))

    assert main._select_default_session_id(sessions) == "session-wrong"


def test_returns_none_for_empty_sessions():
    import main

    assert main._select_default_session_id({}) is None
