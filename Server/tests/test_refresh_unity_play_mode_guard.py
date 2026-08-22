"""refresh_unity(allow_during_play=...) plumbing for issue #67.

The Unity-side guard (RefreshUnity.cs) refuses a refresh/compile while Play
mode is active unless the caller passes allow_during_play=true -- forcing a
domain reload mid-Play permanently disposes the DOTS Default World for the
rest of the session. These tests only cover the Python-side plumbing: the
parameter is forwarded to Unity, defaults to False, and a "play_mode_active"
rejection is surfaced to the caller as a normal (non-retried) failure rather
than being mistaken for the "connection lost because a reload was triggered"
success path refresh_unity treats specially for compile="request".
"""
import pytest

from .integration.test_helpers import DummyContext

INSTANCE = "UnityMCPTests@cc8756d4cce0805a"


@pytest.mark.asyncio
async def test_allow_during_play_defaults_to_false_and_is_forwarded(monkeypatch):
    from services.tools.refresh_unity import refresh_unity
    import services.tools.refresh_unity as refresh_mod

    ctx = DummyContext()
    await ctx.set_state("unity_instance", INSTANCE)

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        if command_type == "refresh_unity":
            captured["params"] = params
            return {"success": True, "message": "Refreshed."}
        raise ValueError(f"Unexpected command: {command_type}")

    monkeypatch.setattr(refresh_mod.unity_transport,
                        "send_with_unity_instance", fake_send_with_unity_instance)

    await refresh_unity(ctx, mode="force", scope="all", compile="none", wait_for_ready=True)

    assert captured["params"]["allow_during_play"] is False


@pytest.mark.asyncio
async def test_allow_during_play_true_is_forwarded(monkeypatch):
    from services.tools.refresh_unity import refresh_unity
    import services.tools.refresh_unity as refresh_mod

    ctx = DummyContext()
    await ctx.set_state("unity_instance", INSTANCE)

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        if command_type == "refresh_unity":
            captured["params"] = params
            return {"success": True, "message": "Refreshed."}
        raise ValueError(f"Unexpected command: {command_type}")

    monkeypatch.setattr(refresh_mod.unity_transport,
                        "send_with_unity_instance", fake_send_with_unity_instance)

    await refresh_unity(ctx, mode="force", scope="all", compile="none",
                         wait_for_ready=True, allow_during_play=True)

    assert captured["params"]["allow_during_play"] is True


@pytest.mark.asyncio
async def test_play_mode_active_rejection_is_not_retried_or_masked_as_success(monkeypatch):
    """A play_mode_active refusal must surface as a real failure -- not be
    mistaken for the "connection lost because compile triggered a reload"
    success path, and not be silently retried like a reloading-rejection."""
    from services.tools.refresh_unity import refresh_unity
    import services.tools.refresh_unity as refresh_mod

    ctx = DummyContext()
    await ctx.set_state("unity_instance", INSTANCE)

    refresh_calls = []

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        if command_type == "refresh_unity":
            refresh_calls.append(params)
            return {
                "success": False,
                "error": "play_mode_active",
                "data": {
                    "reason": "play_mode_active",
                    "message": "refresh_unity was refused because Play mode is active.",
                },
            }
        raise ValueError(f"Unexpected command: {command_type}")

    monkeypatch.setattr(refresh_mod.unity_transport,
                        "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await refresh_unity(ctx, mode="force", scope="all", compile="request", wait_for_ready=True)
    payload = resp.model_dump() if hasattr(resp, "model_dump") else resp

    # Not retried: exactly one attempt, even though compile="request" would
    # normally retry a "connection lost" / "reloading" rejection.
    assert len(refresh_calls) == 1
    assert payload["success"] is False
