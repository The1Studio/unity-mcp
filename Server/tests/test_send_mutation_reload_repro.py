"""Repro: send_mutation never re-sends on the REAL reloading rejection.

The real preflight rejection produced by UnityConnection.send_command is
    MCPResponse(success=False, error="Unity is reloading; please retry", hint="retry")
-- an MCPResponse object with data=None. is_reloading_rejection() used to require
a dict AND data["reason"] == "reloading", so it returned False and the re-send
branch in send_mutation was never taken.
"""
import pytest

from models import MCPResponse
from services.tools.refresh_unity import is_reloading_rejection, send_mutation


class _Ctx:
    """Minimal context stand-in (send_mutation only passes it to wait_for_editor_ready)."""


# The exact object built at transport/legacy/unity_connection.py:365-369
REAL_PREFLIGHT_REJECTION = MCPResponse(
    success=False,
    error="Unity is reloading; please retry",
    hint="retry",
)


def test_is_reloading_rejection_matches_real_preflight_object():
    assert is_reloading_rejection(REAL_PREFLIGHT_REJECTION) is True


def test_is_reloading_rejection_matches_real_preflight_dump():
    assert is_reloading_rejection(REAL_PREFLIGHT_REJECTION.model_dump()) is True


@pytest.mark.asyncio
async def test_send_mutation_resends_on_real_preflight_rejection(monkeypatch):
    """First send hits the real reloading rejection -> mutation MUST be re-sent."""
    from services.tools import refresh_unity as mod

    calls = 0

    async def fake_send(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls == 1:
            return REAL_PREFLIGHT_REJECTION
        return {"success": True, "data": {"retried": True}}

    monkeypatch.setattr(mod.unity_transport, "send_with_unity_instance", fake_send)

    resp = await send_mutation(_Ctx(), None, "manage_script", {"action": "create"})

    assert calls == 2, "reloading rejection must trigger exactly one re-send"
    assert resp == {"success": True, "data": {"retried": True}}
