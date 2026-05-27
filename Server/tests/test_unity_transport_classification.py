"""
Tests for per-exception classification in unity_transport.send_with_unity_instance.

Each test mocks PluginHub.send_command_for_instance to raise a specific
exception class and asserts the classified MCPResponse fields come back
with the expected error, hint, and (where applicable) message.
"""

import asyncio
import pytest
from unittest.mock import AsyncMock, patch

from transport.unity_transport import send_with_unity_instance


async def _noop_send_fn(*args, **kwargs):  # pragma: no cover - unused
    return None


@pytest.fixture(autouse=True)
def _force_http_transport(monkeypatch):
    """All classification tests run on the HTTP branch."""
    from core.config import config as global_config
    monkeypatch.setattr(global_config, "transport_mode", "http")
    monkeypatch.setattr(global_config, "http_remote_hosted", False)
    yield


@pytest.mark.asyncio
async def test_timeout_error_is_classified_as_unity_request_timeout():
    with patch(
        "transport.unity_transport.PluginHub.send_command_for_instance",
        new_callable=AsyncMock,
    ) as mock_send:
        mock_send.side_effect = asyncio.TimeoutError()

        response = await send_with_unity_instance(
            _noop_send_fn, None, "ping", {},
        )

    assert response["success"] is False
    assert response["error"] == "unity_request_timeout"
    assert response["hint"] == "diagnose_then_retry"
    assert response["message"] is not None
    assert "domain-reload" in response["message"]
    assert "Inspect Unity Editor BEFORE retrying" in response["message"]


@pytest.mark.asyncio
async def test_connection_refused_is_classified_as_unity_unreachable():
    with patch(
        "transport.unity_transport.PluginHub.send_command_for_instance",
        new_callable=AsyncMock,
    ) as mock_send:
        mock_send.side_effect = ConnectionRefusedError("no route")

        response = await send_with_unity_instance(
            _noop_send_fn, None, "ping", {},
        )

    assert response["success"] is False
    assert response["error"] == "unity_unreachable"
    assert response["hint"] == "restart_bridge"
    assert response["message"] is not None
    assert "Do NOT kill or restart the Unity Editor process" in response["message"]


@pytest.mark.asyncio
async def test_connection_reset_is_classified_as_unity_unreachable():
    with patch(
        "transport.unity_transport.PluginHub.send_command_for_instance",
        new_callable=AsyncMock,
    ) as mock_send:
        mock_send.side_effect = ConnectionResetError("peer closed")

        response = await send_with_unity_instance(
            _noop_send_fn, None, "ping", {},
        )

    assert response["success"] is False
    assert response["error"] == "unity_unreachable"
    assert response["hint"] == "restart_bridge"


@pytest.mark.asyncio
async def test_generic_runtime_error_preserves_backward_compat_wrapping():
    with patch(
        "transport.unity_transport.PluginHub.send_command_for_instance",
        new_callable=AsyncMock,
    ) as mock_send:
        mock_send.side_effect = RuntimeError("custom message")

        response = await send_with_unity_instance(
            _noop_send_fn, None, "ping", {},
        )

    assert response["success"] is False
    assert response["error"] == "custom message"
    assert response["hint"] == "retry"


@pytest.mark.asyncio
async def test_empty_str_exception_falls_back_to_type_name():
    class _SilentError(Exception):
        def __str__(self) -> str:
            return ""

    with patch(
        "transport.unity_transport.PluginHub.send_command_for_instance",
        new_callable=AsyncMock,
    ) as mock_send:
        mock_send.side_effect = _SilentError()

        response = await send_with_unity_instance(
            _noop_send_fn, None, "ping", {},
        )

    assert response["success"] is False
    assert response["error"] == "_SilentError"
    assert response["hint"] == "retry"
