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
