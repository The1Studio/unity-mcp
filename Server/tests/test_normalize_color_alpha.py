"""Regression tests for normalize_color default-alpha scaling.

Bug: when RGB was given in 0-255 scale with float output and no explicit alpha,
the default opaque alpha (1.0) was appended in float scale, then _to_output_range
divided the WHOLE list by 255 (because RGB > 1) — collapsing alpha 1.0 -> ~0.004,
i.e. a fully-opaque color rendered nearly transparent. The default alpha must be
appended in the RGB's own scale so the range conversion lands on fully-opaque.
"""

from services.tools.utils import normalize_color


def _approx(got, want, tol=1e-6):
    return got is not None and len(got) == len(want) and all(abs(a - b) <= tol for a, b in zip(got, want))


def test_255_rgb_float_output_stays_opaque():
    # The core regression: opaque orange in 0-255 must NOT become transparent.
    color, err = normalize_color([255, 128, 0], "float")
    assert err is None
    assert _approx(color, [1.0, 128 / 255, 0.0, 1.0]), color
    assert color[3] == 1.0  # alpha fully opaque, not ~0.004


def test_255_rgb_dict_float_output_stays_opaque():
    color, err = normalize_color({"r": 255, "g": 128, "b": 0}, "float")
    assert err is None
    assert _approx(color, [1.0, 128 / 255, 0.0, 1.0]), color


def test_normalized_rgb_float_output_unchanged():
    color, err = normalize_color([1.0, 0.5, 0.0], "float")
    assert err is None
    assert _approx(color, [1.0, 0.5, 0.0, 1.0]), color


def test_255_rgb_int_output_unchanged():
    color, err = normalize_color([255, 128, 0], "int")
    assert err is None
    assert color == [255, 128, 0, 255]


def test_normalized_rgb_int_output_becomes_255_opaque():
    color, err = normalize_color([0.5, 0.5, 0.5], "int")
    assert err is None
    assert color == [128, 128, 128, 255]


def test_explicit_alpha_is_respected():
    # Explicit alpha (any scale) must survive untouched by the default-alpha path.
    assert _approx(normalize_color([255, 128, 0, 255], "float")[0], [1.0, 128 / 255, 0.0, 1.0])
    assert _approx(normalize_color([1.0, 0.5, 0.0, 0.5], "float")[0], [1.0, 0.5, 0.0, 0.5])


def test_hex_float_output_stays_opaque():
    color, err = normalize_color("#FF8000", "float")
    assert err is None
    assert _approx(color, [1.0, 128 / 255, 0.0, 1.0]), color
