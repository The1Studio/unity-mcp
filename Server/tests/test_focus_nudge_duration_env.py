import importlib
import pytest

@pytest.mark.parametrize("consecutive, expected", [(0, 6.0), (1, 10.0), (2, 16.0), (3, 24.0)])
def test_nudge_duration_env_var_scales_ladder(monkeypatch, consecutive, expected):
    monkeypatch.setenv("UNITY_MCP_NUDGE_DURATION_S", "6.0")
    from utils import focus_nudge
    focus_nudge = importlib.reload(focus_nudge)
    try:
        focus_nudge._consecutive_nudges = consecutive
        assert focus_nudge._get_current_focus_duration() == expected
    finally:
        monkeypatch.delenv("UNITY_MCP_NUDGE_DURATION_S", raising=False)
        importlib.reload(focus_nudge)

def test_nudge_duration_unset_env_keeps_base_ladder(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_NUDGE_DURATION_S", raising=False)
    from utils import focus_nudge
    focus_nudge = importlib.reload(focus_nudge)
    observed = []
    for n in range(4):
        focus_nudge._consecutive_nudges = n
        observed.append(focus_nudge._get_current_focus_duration())
    focus_nudge._consecutive_nudges = 0
    assert observed == [3.0, 5.0, 8.0, 12.0]
