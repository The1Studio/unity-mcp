"""Unit tests for transport.instance_selection (the shared cwd-preference helper)."""
from __future__ import annotations

from transport.instance_selection import (
    is_project_unrelated_to_cwd,
    normalize_project_root,
    select_sole_match_by_cwd,
)


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


# --- is_project_unrelated_to_cwd (#61) -------------------------------------
#
# These pin the REFUSAL state, not just the pass state: the guard exists to say
# "no" to a disjoint project, so a version that can never return True would be
# decoration. Every "related" case below is a launch position we must NOT break.


def test_unrelated_when_project_and_cwd_are_disjoint(tmp_path):
    project = tmp_path / "ProjectA"
    elsewhere = tmp_path / "ProjectB"
    project.mkdir()
    elsewhere.mkdir()

    assert is_project_unrelated_to_cwd(str(project), cwd=str(elsewhere)) is True


def test_related_when_cwd_is_the_project_root(tmp_path):
    project = tmp_path / "ProjectA"
    project.mkdir()

    assert is_project_unrelated_to_cwd(str(project), cwd=str(project)) is False


def test_related_when_cwd_is_inside_the_project(tmp_path):
    nested = tmp_path / "ProjectA" / "Assets" / "Scripts"
    nested.mkdir(parents=True)

    assert is_project_unrelated_to_cwd(
        str(tmp_path / "ProjectA"), cwd=str(nested)) is False


def test_related_when_project_is_inside_cwd(tmp_path):
    """Monorepo / parent-dir launch: the client's cwd contains the Unity project."""
    project = tmp_path / "repo" / "UnityProject"
    project.mkdir(parents=True)

    assert is_project_unrelated_to_cwd(
        str(project), cwd=str(tmp_path / "repo")) is False


def test_related_when_project_path_reports_the_assets_folder(tmp_path):
    """stdio discovery reports the Assets folder, not the project root."""
    project = tmp_path / "repo" / "UnityProject"
    assets = project / "Assets"
    assets.mkdir(parents=True)

    assert is_project_unrelated_to_cwd(
        str(assets), cwd=str(tmp_path / "repo")) is False


def test_not_unrelated_when_project_path_is_unknown(tmp_path):
    """Undeterminable must never block a selection the old code would have made."""
    assert is_project_unrelated_to_cwd(None, cwd=str(tmp_path)) is False
    assert is_project_unrelated_to_cwd("", cwd=str(tmp_path)) is False


def test_sibling_prefix_paths_are_unrelated(tmp_path):
    """/work/Game must not count as containing /work/GameTools (string-prefix trap)."""
    project = tmp_path / "GameTools"
    cwd = tmp_path / "Game"
    project.mkdir()
    cwd.mkdir()

    assert is_project_unrelated_to_cwd(str(project), cwd=str(cwd)) is True


def test_defaults_to_os_getcwd(tmp_path, monkeypatch):
    project = tmp_path / "ProjectA"
    elsewhere = tmp_path / "ProjectB"
    project.mkdir()
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    assert is_project_unrelated_to_cwd(str(project)) is True
