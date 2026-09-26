"""Gate: the two shipped copies of `unity-mcp-skill` must not drift (issue #110).

The skill ships twice by design, because two different consumers fetch it:

* `unity-mcp-skill/` (top level, canonical) — the repo's own copy.
* `.claude/skills/unity-mcp-skill/` — the path Unity's skill-sync window installs from
  (`SkillSyncService.SkillSubdir`, `McpForUnitySkillInstaller.GetDefaultInstallDir`).

Nothing kept them in step, so they silently diverged: the canonical copy gained a
Physics row and corrected three `open_prefab_stage` examples to `manage_prefabs`, while
the `.claude/skills/` copy kept the old text *and* omitted two reference files its own
`SKILL.md` cites. Every consumer whose installer synced the `.claude/skills/` path
inherited 4 dead relative links plus 2 missing-reference citations, which is a
permanent core doctor #60/#61 FAIL on the installed machine.

The fix that issue asked for is a one-time content sync — but a one-time sync regrows,
because the next doc update edits one copy. This gate is the half that keeps it fixed:
it runs on every PR, so divergence is caught at the change that introduces it rather
than on some later consumer install.
"""

import re
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
CANONICAL = REPO_ROOT / "unity-mcp-skill"
SYNCED = REPO_ROOT / ".claude" / "skills" / "unity-mcp-skill"

# The files a consumer install receives. Deliberately enumerated rather than globbed:
# a glob would pass vacuously if a reference file were dropped from BOTH copies, which
# is exactly the omission this gate exists to catch.
SHIPPED_FILES = (
    "SKILL.md",
    "references/tools-reference.md",
    "references/workflows.md",
    "references/probuilder-guide.md",
    "references/resources-reference.md",
)


def _relative_links(path: Path):
    """Yield (line_number, target) for every non-URL markdown link in `path`."""
    text = path.read_text(encoding="utf-8")
    for match in re.finditer(r"\[([^\]]*)\]\(([^)]+)\)", text):
        target = match.group(2).split("#")[0].strip()
        if not target or "://" in target or target.startswith("mailto:"):
            continue
        yield text[: match.start()].count("\n") + 1, target


def test_both_copies_exist():
    """Guards the gate itself: a missing directory must fail, not silently skip."""
    assert CANONICAL.is_dir(), f"canonical skill missing at {CANONICAL}"
    assert SYNCED.is_dir(), (
        f"synced skill missing at {SYNCED} — SkillSyncService installs from this path"
    )


@pytest.mark.parametrize("rel_path", SHIPPED_FILES)
def test_shipped_file_exists_in_both_copies(rel_path):
    assert (CANONICAL / rel_path).is_file(), f"canonical copy is missing {rel_path}"
    assert (SYNCED / rel_path).is_file(), (
        f"{rel_path} is missing from the synced copy — consumers install this path"
    )


@pytest.mark.parametrize("rel_path", SHIPPED_FILES)
def test_copies_are_byte_identical(rel_path):
    """The drift gate proper.

    If this fails, do NOT edit one side to match by hand — copy the canonical file over
    the synced one, so the diff is one-directional and the canonical copy stays the
    only place a doc change is authored.
    """
    canonical = CANONICAL / rel_path
    synced = SYNCED / rel_path
    if not (canonical.is_file() and synced.is_file()):
        pytest.skip("existence is asserted by test_shipped_file_exists_in_both_copies")

    assert canonical.read_bytes() == synced.read_bytes(), (
        f"{rel_path} has drifted between the canonical and synced skill copies. "
        f"Sync with: cp {CANONICAL / rel_path} {SYNCED / rel_path}"
    )


@pytest.mark.parametrize("rel_path", SHIPPED_FILES)
def test_no_unresolved_relative_links(rel_path):
    """Core doctor #60's rule, asserted at the source so it cannot reach a consumer.

    Checked against BOTH copies: a link can be broken in the synced copy alone (as it
    was — the synced `SKILL.md` cited two files only the canonical copy carried).
    """
    for root in (CANONICAL, SYNCED):
        path = root / rel_path
        if not path.is_file():
            continue
        broken = [
            (line, target)
            for line, target in _relative_links(path)
            if not (path.parent / target).exists()
        ]
        assert not broken, (
            f"{path.relative_to(REPO_ROOT)} has unresolved relative links: "
            + ", ".join(f"line {n} -> {t}" for n, t in broken)
        )


def test_synced_copy_carries_every_cited_reference():
    """Core doctor #61's rule: every `references/*.md` a SKILL.md names must ship."""
    skill_md = SYNCED / "SKILL.md"
    cited = {
        target
        for _, target in _relative_links(skill_md)
        if target.startswith("references/")
    }
    assert cited, "no reference citations found — the parser or the SKILL.md changed"

    missing = sorted(
        target for target in cited if not (SYNCED / target).is_file()
    )
    assert not missing, (
        f"the synced SKILL.md cites reference files it does not ship: {missing}. "
        "This is the exact shape of the consumer doctor #60/#61 FAIL in issue #110."
    )
