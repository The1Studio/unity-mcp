"""Shared helper for disambiguating multiple connected Unity instances by cwd.

With more than one Unity Editor connected (multi-project workflows), several
call sites used to fall back to "just pick the first session" when no explicit
``unity_instance`` was requested. That silently routes commands to whichever
project happened to register first -- a call can succeed against the *wrong*
project with no error at all.

This module centralises the fix: when exactly one connected instance's project
directory contains the server process's current working directory (the
directory the MCP client -- e.g. Claude Code -- was launched from), prefer
that instance. On zero or more than one match, return ``None`` so the caller
falls back to its previous (explicit/first-available) behaviour rather than
guessing.
"""

from __future__ import annotations

import os
from typing import Iterable


def normalize_project_root(project_path: str | None) -> str | None:
    """Normalize a Unity project path to its project-root directory.

    Some callers report the ``Assets`` subfolder rather than the project root;
    strip a trailing ``Assets`` segment before resolving. Returns ``None`` when
    ``project_path`` is missing or cannot be resolved on this filesystem.
    """
    if not project_path:
        return None
    normalized = project_path.rstrip("/\\")
    if not normalized:
        return None
    if os.path.basename(normalized) == "Assets":
        parent = os.path.dirname(normalized)
        normalized = parent or normalized
    try:
        return os.path.realpath(normalized)
    except OSError:
        return None


def select_sole_match_by_cwd(
    candidates: Iterable[tuple[str, str | None]],
    cwd: str | None = None,
) -> str | None:
    """Return the id of the single candidate whose project dir contains ``cwd``.

    Args:
        candidates: iterable of ``(candidate_id, project_path)`` pairs.
        cwd: directory to match against; defaults to ``os.getcwd()``.

    Returns:
        The matching candidate id when exactly one candidate's (normalized)
        project directory is ``cwd`` or an ancestor of it. ``None`` on zero or
        multiple matches -- callers should keep their existing default
        behaviour rather than guess between ambiguous candidates.
    """
    if cwd is None:
        try:
            cwd = os.getcwd()
        except OSError:
            return None
    try:
        cwd = os.path.realpath(cwd)
    except OSError:
        return None

    matches: list[str] = []
    for candidate_id, project_path in candidates:
        root = normalize_project_root(project_path)
        if root is None:
            continue
        if cwd == root or cwd.startswith(root + os.sep):
            matches.append(candidate_id)

    if len(matches) == 1:
        return matches[0]
    return None


class CrossProjectAutoSelectError(RuntimeError):
    """Raised when the sole connected instance belongs to an unrelated project.

    Auto-selecting in that situation is the #61 footgun: the call succeeds, it
    is just answered by a Unity Editor the caller never meant to touch. Carrying
    a dedicated type (rather than a bare ``ValueError``) keeps the refusal from
    being swallowed by the broad "auto-select probe failed" handlers around the
    discovery calls, which deliberately degrade to "no instance" on transport
    errors.
    """


def is_project_unrelated_to_cwd(project_path: str | None, cwd: str | None = None) -> bool:
    """Report whether ``project_path`` provably shares no ancestry with ``cwd``.

    "Related" is deliberately wider than :func:`select_sole_match_by_cwd`'s
    containment test: a client is routinely launched from a monorepo root that
    *contains* the Unity project, so an ancestor cwd counts as related too. Only
    genuinely disjoint paths -- neither one an ancestor of the other -- are
    reported unrelated.

    Returns ``False`` whenever the answer cannot be established (missing or
    unresolvable ``project_path``, unreadable cwd), so an unknown path never
    blocks a selection that the previous behaviour would have made.
    """
    root = normalize_project_root(project_path)
    if root is None:
        return False
    if cwd is None:
        try:
            cwd = os.getcwd()
        except OSError:
            return False
    try:
        cwd = os.path.realpath(cwd)
    except OSError:
        return False

    if cwd == root:
        return False
    if cwd.startswith(root + os.sep):
        return False  # cwd sits inside the project
    if root.startswith(cwd + os.sep):
        return False  # the project sits inside cwd (monorepo / parent-dir launch)
    return True
