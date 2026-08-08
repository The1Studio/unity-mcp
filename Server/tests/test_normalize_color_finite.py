"""normalize_color must reject non-finite (NaN / +-Infinity) color components.

Python's json.loads accepts `NaN`/`Infinity` literals, so a JSON-string color
param (a documented input format) can smuggle them in. The scale heuristics use
`c > 1`, which is always False for NaN, so a non-finite value would otherwise
pass straight through to Unity as an invalid Color.
"""
import math

import pytest

from services.tools.utils import normalize_color


@pytest.mark.parametrize("bad", [
    [float("nan"), 0.0, 0.0],
    [float("inf"), 0.0, 0.0],
    [0.0, float("-inf"), 0.0],
    "[NaN, 0.0, 0.0]",            # JSON string — json.loads accepts NaN
    "[Infinity, 0.0, 0.0]",
    {"r": float("nan"), "g": 0.0, "b": 0.0},
    "(nan, 0, 0)",               # tuple-style string
])
def test_non_finite_color_is_rejected(bad):
    color, err = normalize_color(bad, "float")
    assert color is None, f"expected rejection for {bad!r}, got {color!r}"
    assert err is not None


@pytest.mark.parametrize("good,out", [
    ([0.5, 0.2, 0.1], "float"),
    ([255, 128, 0], "int"),
    ("#ff0000", "float"),          # hex path stays finite + valid
    ({"r": 0.1, "g": 0.2, "b": 0.3}, "float"),
])
def test_finite_color_still_accepted(good, out):
    color, err = normalize_color(good, out)
    assert err is None, f"valid color {good!r} was wrongly rejected: {err}"
    assert color is not None
    assert all(math.isfinite(c) for c in color)
