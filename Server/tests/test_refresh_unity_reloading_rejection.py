"""refresh_unity must re-send when Unity REJECTED the command because it is reloading.

Distinct from tests/integration/test_refresh_unity_retry_recovery.py, which covers the
"connection lost AFTER the command was sent" case (error='disconnected'). Here the
transport's pre-send status-file preflight rejects the command outright
(unity_connection.UnityConnection.send_command), so the refresh never reached Unity —
yet refresh_unity used to report success and clear the external-dirty flag, leaving a
silently stale AssetDatabase.
"""
import pytest

from models import MCPResponse
from services.state.external_changes_scanner import (
    ExternalChangesState,
    external_changes_scanner,
)

from .integration.test_helpers import DummyContext


INSTANCE = "UnityMCPTests@cc8756d4cce0805a"


@pytest.mark.asyncio
async def test_refresh_unity_resends_after_reloading_rejection(monkeypatch):
    from services.tools.refresh_unity import refresh_unity
    import services.tools.refresh_unity as refresh_mod

    ctx = DummyContext()
    await ctx.set_state("unity_instance", INSTANCE)

    external_changes_scanner._states[INSTANCE] = ExternalChangesState(
        dirty=True, dirty_since_unix_ms=1
    )

    refresh_calls = []

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        if command_type == "refresh_unity":
            refresh_calls.append(params)
            if len(refresh_calls) == 1:
                # Exact shape produced by UnityConnection.send_command's status-file
                # preflight: the command was NEVER written to the socket.
                return MCPResponse(
                    success=False,
                    error="Unity is reloading; please retry",
                    hint="retry",
                ).model_dump()
            return {"success": True, "message": "Refreshed."}
        if command_type == "get_editor_state":
            return {"success": True, "data": {"advice": {"ready_for_tools": True}}}
        raise ValueError(f"Unexpected command: {command_type}")

    monkeypatch.setattr(refresh_mod.unity_transport,
                        "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await refresh_unity(ctx, mode="force", scope="all", compile="request", wait_for_ready=True)
    payload = resp.model_dump() if hasattr(resp, "model_dump") else resp

    # The refresh was rejected before it ever ran -> it must be re-sent.
    assert len(refresh_calls) == 2, (
        "refresh_unity reported success without re-sending the rejected refresh "
        f"(sent {len(refresh_calls)} time(s))"
    )
    assert payload["success"] is True
