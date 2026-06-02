"""Eviction-notice frame handling on the legacy TCP transport.

When the Unity bridge sends a structured eviction frame (status="evicted"),
the Python frame reader must raise EvictedByOtherClientError instead of
returning the payload as a normal response. Non-eviction frames must
pass through unchanged.
"""

import json
import socket
import struct
import threading
import time
from pathlib import Path

import pytest

from transport.legacy.unity_connection import (
    EvictedByOtherClientError,
    UnityConnection,
)


# locate server src dynamically to match test_transport_framing.py
ROOT = Path(__file__).resolve().parents[2]
candidates = [
    ROOT / "src",
]
SRC = next((p for p in candidates if p.exists()), None)
if SRC is None:
    searched = "\n".join(str(p) for p in candidates)
    pytest.skip(
        "MCP for Unity server source not found. Tried:\n" + searched,
        allow_module_level=True,
    )


def _start_eviction_server(eviction_payload: bytes):
    """Start a TCP server that sends handshake then a single eviction frame."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.bind(("127.0.0.1", 0))
    sock.listen(1)
    port = sock.getsockname()[1]
    ready = threading.Event()

    def _run():
        ready.set()
        conn, _ = sock.accept()
        try:
            conn.sendall(b"WELCOME UNITY-MCP 1 FRAMING=1\n")
            time.sleep(0.02)
            conn.sendall(struct.pack(">Q", len(eviction_payload)) + eviction_payload)
            time.sleep(0.05)
        finally:
            try:
                conn.close()
            except Exception:
                pass
            sock.close()

    threading.Thread(target=_run, daemon=True).start()
    ready.wait()
    return port


def _start_passthrough_server(payload: bytes):
    """Start a TCP server that sends handshake then a normal response frame."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.bind(("127.0.0.1", 0))
    sock.listen(1)
    port = sock.getsockname()[1]
    ready = threading.Event()

    def _run():
        ready.set()
        conn, _ = sock.accept()
        try:
            conn.sendall(b"WELCOME UNITY-MCP 1 FRAMING=1\n")
            time.sleep(0.02)
            conn.sendall(struct.pack(">Q", len(payload)) + payload)
            time.sleep(0.05)
        finally:
            try:
                conn.close()
            except Exception:
                pass
            sock.close()

    threading.Thread(target=_run, daemon=True).start()
    ready.wait()
    return port


def test_eviction_frame_raises_typed_exception():
    eviction = {
        "status": "evicted",
        "reason": "stdio_single_client_stomped",
        "error": "Another MCP client connected from 127.0.0.1:54321. "
                 "Stdio transport allows only one MCP client at a time; "
                 "this connection has been evicted.",
        "evicted_at_unix_ms": 1748345678901,
        "new_client_endpoint": "127.0.0.1:54321",
    }
    port = _start_eviction_server(json.dumps(eviction).encode("utf-8"))

    conn = UnityConnection(host="127.0.0.1", port=port)
    try:
        assert conn.connect() is True
        assert conn.use_framing is True
        with pytest.raises(EvictedByOtherClientError) as excinfo:
            conn.receive_full_response(conn.sock)

        err = excinfo.value
        assert "Another MCP client connected" in str(err)
        assert err.reason == "stdio_single_client_stomped"
        assert err.new_client_endpoint == "127.0.0.1:54321"
        assert err.evicted_at_unix_ms == 1748345678901
        assert err.payload["status"] == "evicted"
    finally:
        conn.disconnect()


def test_non_eviction_frame_passes_through():
    payload = b'{"status":"success","result":{"message":"pong"}}'
    port = _start_passthrough_server(payload)

    conn = UnityConnection(host="127.0.0.1", port=port)
    try:
        assert conn.connect() is True
        resp = conn.receive_full_response(conn.sock)
        assert resp == payload
        # Sanity: regular responses must NOT trip the eviction branch even when
        # they happen to be JSON dicts.
        decoded = json.loads(resp.decode("utf-8"))
        assert decoded["status"] == "success"
    finally:
        conn.disconnect()


def test_non_json_payload_passes_through():
    # Defensive: a non-JSON payload (e.g. binary, malformed UTF-8) must not
    # raise inside the eviction-detection branch — it should be returned as-is
    # so the existing JSON-validation path in send_command surfaces the right
    # error.
    payload = b'\x80\x81\x82 not valid utf-8 or json'
    port = _start_passthrough_server(payload)

    conn = UnityConnection(host="127.0.0.1", port=port)
    try:
        assert conn.connect() is True
        resp = conn.receive_full_response(conn.sock)
        assert resp == payload
    finally:
        conn.disconnect()


def test_evicted_error_is_connection_error_subclass():
    # The exception MUST subclass ConnectionError so existing connection-error
    # handling paths in send_command's retry loop continue to work without
    # explicit catches.
    err = EvictedByOtherClientError("evicted", {"reason": "stdio_single_client_stomped"})
    assert isinstance(err, ConnectionError)
    assert err.reason == "stdio_single_client_stomped"
    assert err.new_client_endpoint is None
    assert err.evicted_at_unix_ms is None
