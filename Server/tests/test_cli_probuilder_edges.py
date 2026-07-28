"""Regression: probuilder _parse_edges_param must reject non-array JSON cleanly.

Bug: _parse_edges_param did `parsed = json.loads(edges)` then indexed
`parsed[0]` — but json.loads can yield a non-list (int/dict/bool), so
`probuilder extrude-edges/bevel-edges --edges 5` crashed with an uncaught
TypeError ('int' object is not subscriptable) and `--edges '{"a":1}'` with a
KeyError. It now raises a clean SystemExit with a helpful message, matching the
existing invalid-JSON path.
"""

import pytest

from cli.commands.probuilder import _parse_edges_param


@pytest.mark.parametrize("bad", ["5", '{"a":1}', "true", "3.14"])
def test_non_array_edges_exit_cleanly(bad):
    # Pre-fix: TypeError / KeyError (uncaught). Post-fix: SystemExit(1).
    with pytest.raises(SystemExit):
        _parse_edges_param(bad)


def test_invalid_json_still_exits():
    with pytest.raises(SystemExit):
        _parse_edges_param("not-json")


def test_flat_index_list_parsed():
    assert _parse_edges_param("[0, 1, 2]") == {"edgeIndices": [0, 1, 2]}


def test_vertex_pair_list_parsed():
    assert _parse_edges_param('[{"a":0,"b":1}]') == {"edges": [{"a": 0, "b": 1}]}


def test_empty_list_is_edge_indices():
    assert _parse_edges_param("[]") == {"edgeIndices": []}
