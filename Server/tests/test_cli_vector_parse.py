"""Regression: comma-separated vector/color CLI options must reject malformed
input cleanly instead of crashing with an uncaught ValueError.

Bug: physics.py (14 sites) and graphics.py (5 sites) built params with a bare
`[float(x) for x in origin.split(",")]` (and `int(x)` for graphics order), so a
plausible typo like `--origin "0,0,"` (trailing comma) or `--origin "0,,0"`
(empty component) raised an uncaught ValueError — a raw traceback with no message
to the user. The codebase's own prefab._parse_vector3 shows the intended guarded
pattern (click.BadParameter). Fixed via parsers.parse_float_list/parse_int_list.
"""

import pytest
import click
from click.testing import CliRunner

from cli.utils.parsers import parse_float_list, parse_int_list
from cli.commands.physics import physics
from cli.commands.graphics import graphics


# ── unit tests on the shared helpers ──────────────────────────────────────────

def test_parse_float_list_valid():
    assert parse_float_list("0,1.5,-2") == [0.0, 1.5, -2.0]
    assert parse_float_list("1, 2 , 3") == [1.0, 2.0, 3.0]  # whitespace tolerated


@pytest.mark.parametrize("bad", ["0,0,", "0,,0", "a,b,c", "", "1,x"])
def test_parse_float_list_bad_raises_badparameter(bad):
    with pytest.raises(click.BadParameter):
        parse_float_list(bad, "origin")


def test_parse_int_list_valid_and_bad():
    assert parse_int_list("2,0,1") == [2, 0, 1]
    with pytest.raises(click.BadParameter):
        parse_int_list("2,0,", "order")


# ── CLI-level: malformed input must NOT surface an uncaught ValueError ─────────

def test_physics_raycast_malformed_origin_no_crash():
    r = CliRunner().invoke(physics, ["raycast", "--origin", "0,0,", "--direction", "0,-1,0"])
    # Pre-fix: r.exception is an uncaught ValueError with empty output.
    assert not isinstance(r.exception, ValueError)
    assert r.exit_code != 0


def test_graphics_malformed_color_no_crash():
    r = CliRunner().invoke(graphics, ["skybox-set-ambient", "--color", "1,1,"])
    assert not isinstance(r.exception, ValueError)
    assert r.exit_code != 0


def test_graphics_malformed_order_no_crash():
    r = CliRunner().invoke(graphics, ["feature-reorder", "--order", "2,0,"])
    assert not isinstance(r.exception, ValueError)
    assert r.exit_code != 0
