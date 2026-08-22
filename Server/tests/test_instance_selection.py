"""Unit tests for transport.instance_selection (the shared cwd-preference helper)."""
from __future__ import annotations

from transport.instance_selection import normalize_project_root, select_sole_match_by_cwd


def test_normalize_project_root_strips_trailing_assets(tmp_path):
    project = tmp_path / "MyProject"
    assets = project / "Assets"
    assets.mkdir(parents=True)

    assert normalize_project_root(str(assets)) == normalize_project_root(str(project))


def test_normalize_project_root_handles_trailing_separator(tmp_path):
    project = tmp_path / "MyProject"
    project.mkdir()

    assert normalize_project_root(str(project) + "/") == normalize_project_root(str(project))


def test_normalize_project_root_none_for_missing_input():
    assert normalize_project_root(None) is None
    assert normalize_project_root("") is None


def test_select_sole_match_by_cwd_exact_match(tmp_path):
    project_a = tmp_path / "A"
    project_b = tmp_path / "B"
    project_a.mkdir()
    project_b.mkdir()

    result = select_sole_match_by_cwd(
        [("id-a", str(project_a)), ("id-b", str(project_b))],
        cwd=str(project_b),
    )

    assert result == "id-b"


def test_select_sole_match_by_cwd_nested_cwd(tmp_path):
    project_a = tmp_path / "A"
    project_b = tmp_path / "B"
    nested = project_b / "Assets" / "Scripts"
    nested.mkdir(parents=True)
    project_a.mkdir()

    result = select_sole_match_by_cwd(
        [("id-a", str(project_a)), ("id-b", str(project_b))],
        cwd=str(nested),
    )

    assert result == "id-b"


def test_select_sole_match_by_cwd_returns_none_on_zero_matches(tmp_path):
    project_a = tmp_path / "A"
    project_b = tmp_path / "B"
    elsewhere = tmp_path / "elsewhere"
    project_a.mkdir()
    project_b.mkdir()
    elsewhere.mkdir()

    result = select_sole_match_by_cwd(
        [("id-a", str(project_a)), ("id-b", str(project_b))],
        cwd=str(elsewhere),
    )

    assert result is None


def test_select_sole_match_by_cwd_returns_none_on_ambiguous_matches(tmp_path):
    shared = tmp_path / "shared"
    shared.mkdir()

    result = select_sole_match_by_cwd(
        [("id-a", str(shared)), ("id-b", str(shared))],
        cwd=str(shared),
    )

    assert result is None


def test_select_sole_match_by_cwd_ignores_candidates_with_no_project_path(tmp_path):
    project_b = tmp_path / "B"
    project_b.mkdir()

    result = select_sole_match_by_cwd(
        [("id-a", None), ("id-b", str(project_b))],
        cwd=str(project_b),
    )

    assert result == "id-b"


def test_select_sole_match_by_cwd_defaults_to_os_getcwd(tmp_path, monkeypatch):
    project = tmp_path / "P"
    project.mkdir()
    monkeypatch.chdir(project)

    result = select_sole_match_by_cwd([("id-a", str(project))])

    assert result == "id-a"
