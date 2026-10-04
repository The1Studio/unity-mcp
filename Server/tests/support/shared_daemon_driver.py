"""Driver for the shared-daemon test (run by ``tests/test_shared_daemon_multi_editor.py``).

Why a separate interpreter: ``tests/integration/conftest.py`` replaces ``fastmcp``
with a stub for the whole pytest session, so a real FastMCP server and real MCP
clients cannot live in the pytest process. This script runs in a fresh
interpreter with the real libraries.

It starts the REAL command users run::

    python src/main.py --transport http --http-host 127.0.0.1 --http-port <port>

then attaches fake Unity Editors over a real WebSocket to ``/hub/plugin`` (the
plugin wire protocol: welcome / register / registered / execute /
command_result / ping / pong) and drives it with real ``fastmcp.Client``
streamable-HTTP sessions, each of which gets its own ``mcp-session-id`` exactly
as two Claude Code sessions would. Nothing below the MCP layer is mocked.

Usage: shared_daemon_driver.py <scenario> <workdir>
Exit 0 = scenario held; non-zero = assertion/trace on stderr.
"""
from __future__ import annotations

import asyncio
import json
import os
import socket
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

import websockets
from fastmcp import Client
from fastmcp.client.transports import StreamableHttpTransport

SERVER_DIR = Path(__file__).resolve().parents[2]


class FakeEditor:
    def __init__(self, port: int, name: str, project_hash: str, project_path: str):
        self.url = f"ws://127.0.0.1:{port}/hub/plugin"
        self.name = name
        self.hash = project_hash
        self.path = project_path
        self.received: list[dict] = []
        self._ws = None
        self._task: asyncio.Task | None = None

    async def connect(self) -> None:
        self._ws = await websockets.connect(self.url)
        welcome = json.loads(await self._ws.recv())
        assert welcome["type"] == "welcome", welcome
        await self._ws.send(json.dumps({
            "type": "register",
            "project_name": self.name,
            "project_hash": self.hash,
            "unity_version": "6000.0.0f1",
            "project_path": self.path,
        }))
        registered = json.loads(await self._ws.recv())
        assert registered["type"] == "registered", registered
        self._task = asyncio.create_task(self._serve())

    async def _serve(self) -> None:
        try:
            async for raw in self._ws:
                msg = json.loads(raw)
                if msg.get("type") == "ping":
                    await self._ws.send(json.dumps({"type": "pong"}))
                elif msg.get("type") == "execute":
                    self.received.append(msg)
                    await self._ws.send(json.dumps({
                        "type": "command_result",
                        "id": msg["id"],
                        "result": {
                            "success": True,
                            "message": "ok",
                            "data": {"answered_by": self.name},
                        },
                    }))
        except websockets.ConnectionClosed:
            pass

    async def disconnect(self) -> None:
        if self._ws is not None:
            await self._ws.close()
        if self._task is not None:
            await asyncio.wait({self._task}, timeout=2)


class Daemon:
    """The real ``main.py --transport http`` process."""

    def __init__(self, workdir: Path, cwd: Path):
        with socket.socket() as s:
            s.bind(("127.0.0.1", 0))
            self.port = s.getsockname()[1]
        env = dict(os.environ)
        env.update({
            "HOME": str(workdir / "home"),
            "USERPROFILE": str(workdir / "home"),
            "UNITY_MCP_SKIP_STARTUP_CONNECT": "1",
            "DISABLE_TELEMETRY": "true",
            "UNITY_MCP_SESSION_RESOLVE_MAX_WAIT_S": "0",
            "PYTHONUNBUFFERED": "1",
        })
        env.pop("UNITY_MCP_DEFAULT_INSTANCE", None)
        (workdir / "home").mkdir(parents=True, exist_ok=True)
        self.log = workdir / "daemon.log"
        self._log_fh = open(self.log, "w")
        self.proc = subprocess.Popen(
            [sys.executable, str(SERVER_DIR / "src" / "main.py"),
             "--transport", "http", "--http-host", "127.0.0.1",
             "--http-port", str(self.port)],
            cwd=str(cwd), env=env, stdout=self._log_fh, stderr=subprocess.STDOUT,
        )

    def wait_ready(self, timeout: float = 60.0) -> None:
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if self.proc.poll() is not None:
                raise RuntimeError(f"daemon exited early: {self.log.read_text()[-2000:]}")
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{self.port}/health", timeout=1):
                    return
            except Exception:
                time.sleep(0.2)
        raise RuntimeError(f"daemon not ready: {self.log.read_text()[-2000:]}")

    def stop(self) -> None:
        if self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.proc.kill()
        self._log_fh.close()

    @property
    def mcp_url(self) -> str:
        return f"http://127.0.0.1:{self.port}/mcp"


def client(d: Daemon) -> Client:
    return Client(StreamableHttpTransport(d.mcp_url))


async def scene(c: Client, **extra):
    """Call a tool that routes to an Editor; return (is_error, structured, text)."""
    res = await c.call_tool(
        "manage_scene", {"action": "get_active", **extra}, raise_on_error=False)
    text = res.content[0].text if res.content else ""
    return res.is_error, (res.structured_content or {}), text


async def answered_by(c: Client, **extra) -> str:
    is_error, sc, text = await scene(c, **extra)
    assert not is_error, text
    assert sc.get("success"), sc
    return sc["data"]["answered_by"]


async def instances(c: Client) -> list[str]:
    contents = await c.read_resource("mcpforunity://instances")
    payload = json.loads(contents[0].text)
    return sorted(i["id"] for i in payload["instances"])


A_ID, B_ID = "ProjA@aaaa1111", "ProjB@bbbb2222"


async def two_editors(d: Daemon, a: Path, b: Path):
    ed_a = FakeEditor(d.port, "ProjA", "aaaa1111", str(a))
    ed_b = FakeEditor(d.port, "ProjB", "bbbb2222", str(b))
    await ed_a.connect()
    await ed_b.connect()
    return ed_a, ed_b


# ---------------------------------------------------------------- scenarios
async def routes_by_selector(d, a, b):
    """(a) Name@hash, full hash and unique hash prefix each reach the right editor."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        assert await answered_by(c, unity_instance=A_ID) == "ProjA"
        assert await answered_by(c, unity_instance=B_ID) == "ProjB"
        assert await answered_by(c, unity_instance="bbbb2222") == "ProjB"
        assert await answered_by(c, unity_instance="aaaa") == "ProjA"
    assert len(ed_a.received) == 2 and len(ed_b.received) == 2, (
        len(ed_a.received), len(ed_b.received))


async def port_selector_rejected(d, a, b):
    """(a) A port is a stdio-bridge concept: HTTP mode must refuse, not guess."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        is_error, _sc, text = await scene(c, unity_instance="6401")
        assert is_error, text
        assert "not supported in HTTP transport" in text, text
    assert not ed_a.received and not ed_b.received


async def no_selection_lists_instances(d, a, b):
    """(b) >1 editor, no selection, server cwd matches neither: clear error, no guess."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        is_error, sc, text = await scene(c)
        blob = text + json.dumps(sc)
        assert is_error or sc.get("success") is False, blob
        assert A_ID in blob and B_ID in blob, blob
        assert "unity_instance" in blob, blob
    assert not ed_a.received and not ed_b.received, "a call was guessed onto an editor"


async def cwd_match_selects(d, a, b):
    """(b) Daemon launched inside ProjA: no selection routes to ProjA, never ProjB."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        assert await answered_by(c) == "ProjA"
    assert not ed_b.received


async def concurrent_sessions(d, a, b):
    """(c) Two MCP sessions, one daemon: each sticks to its own editor, no eviction."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c1, client(d) as c2:
        r1 = await c1.call_tool("set_active_instance", {"instance": A_ID}, raise_on_error=False)
        r2 = await c2.call_tool("set_active_instance", {"instance": B_ID}, raise_on_error=False)
        assert not r1.is_error and not r2.is_error, (r1, r2)
        for _ in range(3):
            got = await asyncio.gather(answered_by(c1), answered_by(c2))
            assert got == ["ProjA", "ProjB"], got
        # a per-call override on c2 must not disturb c1's sticky choice
        assert await answered_by(c2, unity_instance=A_ID) == "ProjA"
        assert await answered_by(c1) == "ProjA"
        assert await instances(c1) == [A_ID, B_ID]


async def second_client_leaving(d, a, b):
    """(c) A second client connecting and leaving takes nothing down with it."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c1:
        assert await instances(c1) == [A_ID, B_ID]
        async with client(d) as c2:
            assert await instances(c2) == [A_ID, B_ID]
        assert await instances(c1) == [A_ID, B_ID]
        assert await answered_by(c1, unity_instance=B_ID) == "ProjB"


async def disconnect_reflected(d, a, b):
    """(d) An editor dropping off shows up in mcpforunity://instances; its id then errors."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        assert await instances(c) == [A_ID, B_ID]
        await ed_b.disconnect()
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline and await instances(c) != [A_ID]:
            await asyncio.sleep(0.1)
        assert await instances(c) == [A_ID], await instances(c)
        is_error, sc, text = await scene(c, unity_instance=B_ID)
        assert is_error and B_ID in text, (is_error, text)
        assert not ed_a.received, "call for the gone editor was rerouted to the other"


async def schema_advertises_unity_instance(d, a, b):
    """#101: every Editor-routed tool schema lists optional unity_instance; meta-tools do not."""
    ed_a, ed_b = await two_editors(d, a, b)
    async with client(d) as c:
        tools = {t.name: t for t in await c.list_tools()}
        scene_schema = tools["manage_scene"].inputSchema
        prop = scene_schema["properties"].get("unity_instance")
        assert prop is not None, list(scene_schema["properties"])
        assert "unity_instance" not in scene_schema.get("required", []), scene_schema
        assert scene_schema.get("additionalProperties") is False, scene_schema
        missing = [n for n, t in tools.items()
                   if n not in ("set_active_instance", "manage_tools",
                                "debug_request_context", "manage_script_capabilities")
                   and "unity_instance" not in t.inputSchema.get("properties", {})]
        assert not missing, f"tools without unity_instance: {missing}"
        assert "unity_instance" not in tools["set_active_instance"].inputSchema["properties"]
        # a schema-validating client accepts the advertised argument
        import jsonschema
        jsonschema.validate({"action": "get_active", "unity_instance": B_ID}, scene_schema)
        jsonschema.validate({"action": "get_active", "unity_instance": None}, scene_schema)
        # and the advertised argument still routes
        assert await answered_by(c, unity_instance=B_ID) == "ProjB"


SCENARIOS = {
    "schema_advertises_unity_instance": (schema_advertises_unity_instance, "elsewhere"),
    "routes_by_selector": (routes_by_selector, "elsewhere"),
    "port_selector_rejected": (port_selector_rejected, "elsewhere"),
    "no_selection_lists_instances": (no_selection_lists_instances, "elsewhere"),
    "cwd_match_selects": (cwd_match_selects, "ProjA/Assets"),
    "concurrent_sessions": (concurrent_sessions, "elsewhere"),
    "second_client_leaving": (second_client_leaving, "elsewhere"),
    "disconnect_reflected": (disconnect_reflected, "elsewhere"),
}


async def run(name: str, workdir: Path) -> None:
    fn, daemon_cwd = SCENARIOS[name]
    a, b = workdir / "ProjA", workdir / "ProjB"
    for p in (a, b, workdir / "elsewhere"):
        (p / "Assets" if p.name.startswith("Proj") else p).mkdir(parents=True, exist_ok=True)
    d = Daemon(workdir, workdir / daemon_cwd)
    try:
        d.wait_ready()
        await asyncio.wait_for(fn(d, a, b), timeout=60)
    finally:
        d.stop()


if __name__ == "__main__":
    asyncio.run(run(sys.argv[1], Path(sys.argv[2]).resolve()))
