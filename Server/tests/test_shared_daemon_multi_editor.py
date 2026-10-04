"""One shared HTTP daemon serving several Unity Editors and several MCP clients.

Each scenario runs ``tests/support/shared_daemon_driver.py`` in a fresh
interpreter (the integration conftest stubs ``fastmcp`` process-wide, which a
real server cannot live under). The driver launches the real
``main.py --transport http`` command, connects fake Unity Editors over a real
WebSocket and drives it with real MCP client sessions.

NOT covered: a real Unity Editor connecting. See ``Server/README.md``.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

DRIVER = Path(__file__).parent / "support" / "shared_daemon_driver.py"

SCENARIOS = [
    "routes_by_selector",          # (a)
    "port_selector_rejected",      # (a)
    "no_selection_lists_instances",  # (b)
    "cwd_match_selects",           # (b)
    "concurrent_sessions",         # (c)
    "second_client_leaving",       # (c)
    "disconnect_reflected",        # (d)
    "schema_advertises_unity_instance",  # #101
]


@pytest.mark.parametrize("scenario", SCENARIOS)
def test_shared_daemon_scenario(scenario, tmp_path):
    proc = subprocess.run(
        [sys.executable, str(DRIVER), scenario, str(tmp_path)],
        capture_output=True, text=True, timeout=180,
    )
    log = tmp_path / "daemon.log"
    daemon_log = log.read_text()[-3000:] if log.exists() else "<no daemon log>"
    assert proc.returncode == 0, (
        f"scenario {scenario!r} failed\n--- driver stderr ---\n{proc.stderr[-3000:]}"
        f"\n--- daemon log tail ---\n{daemon_log}"
    )
