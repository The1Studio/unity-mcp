import re
from services.tools.script_apply_edits import _find_best_anchor_match

CODE = (
    'public class PlayerController : MonoBehaviour\n'
    '{\n'
    '    void Start()\n'
    '    {\n'
    '    }\n'
    '\n'
    '    void Update()\n'
    '    {\n'
    '    }\n'
    '}\n'
)

def test_quantifier_anchor_still_matches():
    pattern = r'^\s{4}void \w+\(\)\s*$'
    raw = list(re.finditer(pattern, CODE, re.MULTILINE))
    assert len(raw) == 2, "sanity: the regex really does match twice"
    match = _find_best_anchor_match(pattern, CODE, re.MULTILINE, prefer_last=True)
    assert match is not None, "anchor has 2 real matches but helper reported none"
    assert match.group(0).strip() == "void Update()"

def test_trailing_ws_quantifier_anchor_still_matches():
    pattern = r'^\s{4}void \w+\(\)\s*'
    assert len(list(re.finditer(pattern, CODE, re.MULTILINE))) == 2
    assert _find_best_anchor_match(pattern, CODE, re.MULTILINE, prefer_last=True) is not None

def test_real_closing_brace_still_uses_heuristic():
    # A genuine closing-brace anchor must still route through the scorer and match.
    pattern = r'^\s*}\s*$'
    m = _find_best_anchor_match(pattern, CODE, re.MULTILINE, prefer_last=True)
    assert m is not None and m.group(0).strip() == "}"
