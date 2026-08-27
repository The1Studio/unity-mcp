import pytest
import sys
import types
from types import SimpleNamespace

from .test_helpers import DummyContext
from core.config import config


class DummyMiddlewareContext:
    def __init__(self, ctx):
        self.fastmcp_context = ctx


@pytest.mark.asyncio
async def test_auto_selects_single_instance_via_pluginhub(monkeypatch):
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return True

        @classmethod
        async def get_sessions(cls):
            raise AssertionError("get_sessions should be stubbed in test")

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(sys.modules, "transport.unity_instance_middleware", raising=False)

    from transport.unity_instance_middleware import UnityInstanceMiddleware, PluginHub as ImportedPluginHub
    assert ImportedPluginHub is plugin_hub.PluginHub

    monkeypatch.setattr(config, "transport_mode", "http")

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"
    middleware_context = DummyMiddlewareContext(ctx)

    call_count = {"sessions": 0}

    async def fake_get_sessions():
        call_count["sessions"] += 1
        return SimpleNamespace(
            sessions={
                "session-1": SimpleNamespace(project="Ramble", hash="deadbeef"),
            }
        )

    monkeypatch.setattr(plugin_hub.PluginHub, "get_sessions", fake_get_sessions)

    selected = await middleware._maybe_autoselect_instance(ctx)

    assert selected == "Ramble@deadbeef"
    assert await middleware.get_active_instance(ctx) == "Ramble@deadbeef"
    assert call_count["sessions"] == 1

    await middleware._inject_unity_instance(middleware_context)

    assert await ctx.get_state("unity_instance") == "Ramble@deadbeef"
    assert call_count["sessions"] == 1


@pytest.mark.asyncio
async def test_auto_selects_single_instance_via_stdio(monkeypatch):
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return False

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(sys.modules, "transport.unity_instance_middleware", raising=False)

    from transport.unity_instance_middleware import UnityInstanceMiddleware, PluginHub as ImportedPluginHub
    assert ImportedPluginHub is plugin_hub.PluginHub

    monkeypatch.setattr(config, "transport_mode", "stdio")

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"
    middleware_context = DummyMiddlewareContext(ctx)

    class PoolStub:
        def discover_all_instances(self, force_refresh=False):
            assert force_refresh is True
            return [SimpleNamespace(id="UnityMCPTests@cc8756d4")]

    unity_connection = types.ModuleType("transport.legacy.unity_connection")
    unity_connection.get_unity_connection_pool = lambda: PoolStub()
    monkeypatch.setitem(sys.modules, "transport.legacy.unity_connection", unity_connection)

    selected = await middleware._maybe_autoselect_instance(ctx)

    assert selected == "UnityMCPTests@cc8756d4"
    assert await middleware.get_active_instance(ctx) == "UnityMCPTests@cc8756d4"

    await middleware._inject_unity_instance(middleware_context)

    assert await ctx.get_state("unity_instance") == "UnityMCPTests@cc8756d4"


@pytest.mark.asyncio
async def test_auto_select_handles_stdio_errors(monkeypatch):
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return False

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(sys.modules, "transport.unity_instance_middleware", raising=False)

    from transport.unity_instance_middleware import UnityInstanceMiddleware, PluginHub as ImportedPluginHub
    assert ImportedPluginHub is plugin_hub.PluginHub

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    class PoolStub:
        def discover_all_instances(self, force_refresh=False):
            raise ConnectionError("stdio unavailable")

    unity_connection = types.ModuleType("transport.legacy.unity_connection")
    unity_connection.get_unity_connection_pool = lambda: PoolStub()
    monkeypatch.setitem(sys.modules, "transport.legacy.unity_connection", unity_connection)

    selected = await middleware._maybe_autoselect_instance(ctx)

    assert selected is None
    assert await middleware.get_active_instance(ctx) is None


@pytest.mark.asyncio
async def test_auto_selects_by_cwd_among_multiple_pluginhub_instances(monkeypatch, tmp_path):
    """#66: with >1 instances connected, prefer the one whose project_path
    contains this process's cwd instead of leaving the caller to guess."""
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return True

        @classmethod
        async def get_sessions(cls):
            raise AssertionError("get_sessions should be stubbed in test")

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(sys.modules, "transport.unity_instance_middleware", raising=False)

    from transport.unity_instance_middleware import UnityInstanceMiddleware, PluginHub as ImportedPluginHub
    assert ImportedPluginHub is plugin_hub.PluginHub

    monkeypatch.setattr(config, "transport_mode", "http")

    project_a = tmp_path / "ProjectA"
    project_b = tmp_path / "ProjectB"
    project_a.mkdir()
    project_b.mkdir()
    monkeypatch.chdir(project_b)

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    async def fake_get_sessions():
        return SimpleNamespace(
            sessions={
                # Inserted first, so a plain "pick one" fallback would land here.
                "session-a": SimpleNamespace(project="ProjectA", hash="aaaaaaaa", project_path=str(project_a)),
                "session-b": SimpleNamespace(project="ProjectB", hash="bbbbbbbb", project_path=str(project_b)),
            }
        )

    monkeypatch.setattr(plugin_hub.PluginHub, "get_sessions", fake_get_sessions)

    selected = await middleware._maybe_autoselect_instance(ctx)

    assert selected == "ProjectB@bbbbbbbb"
    assert await middleware.get_active_instance(ctx) == "ProjectB@bbbbbbbb"


@pytest.mark.asyncio
async def test_no_autoselect_when_multiple_pluginhub_instances_dont_disambiguate(monkeypatch, tmp_path):
    """Zero or ambiguous cwd matches must keep the prior "ask the caller" behaviour."""
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return True

        @classmethod
        async def get_sessions(cls):
            raise AssertionError("get_sessions should be stubbed in test")

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(sys.modules, "transport.unity_instance_middleware", raising=False)

    from transport.unity_instance_middleware import UnityInstanceMiddleware, PluginHub as ImportedPluginHub
    assert ImportedPluginHub is plugin_hub.PluginHub

    monkeypatch.setattr(config, "transport_mode", "http")

    project_a = tmp_path / "ProjectA"
    project_b = tmp_path / "ProjectB"
    elsewhere = tmp_path / "elsewhere"
    project_a.mkdir()
    project_b.mkdir()
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    async def fake_get_sessions():
        return SimpleNamespace(
            sessions={
                "session-a": SimpleNamespace(project="ProjectA", hash="aaaaaaaa", project_path=str(project_a)),
                "session-b": SimpleNamespace(project="ProjectB", hash="bbbbbbbb", project_path=str(project_b)),
            }
        )

    monkeypatch.setattr(plugin_hub.PluginHub, "get_sessions", fake_get_sessions)

    selected = await middleware._maybe_autoselect_instance(ctx)

    assert selected is None
    assert await middleware.get_active_instance(ctx) is None


def _single_session_plugin_hub(monkeypatch, *, project_path):
    """Install a PluginHub stub reporting exactly one session at ``project_path``."""
    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return True

        @classmethod
        async def get_sessions(cls):
            return SimpleNamespace(
                sessions={
                    "session-a": SimpleNamespace(
                        project="ProjectA",
                        hash="aaaaaaaa",
                        project_path=project_path,
                    ),
                }
            )

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(
        sys.modules, "transport.unity_instance_middleware", raising=False)
    monkeypatch.setattr(config, "transport_mode", "http")
    monkeypatch.setattr(config, "allow_cross_project_autoselect", False)


@pytest.mark.asyncio
async def test_refuses_sole_pluginhub_instance_from_an_unrelated_project(monkeypatch, tmp_path):
    """#61: one registered instance is not a licence to drive it.

    The reporting project's Editor bridge failed to compile, so the only session
    on the hub belonged to a different project -- and every tool call was answered
    by it, successfully and silently. Refuse instead, naming both paths.
    """
    project_a = tmp_path / "ProjectA"
    elsewhere = tmp_path / "ProjectB"
    project_a.mkdir()
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    _single_session_plugin_hub(monkeypatch, project_path=str(project_a))
    from transport.unity_instance_middleware import (
        UnityInstanceMiddleware,
        CrossProjectAutoSelectError,
    )

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    with pytest.raises(CrossProjectAutoSelectError) as excinfo:
        await middleware._maybe_autoselect_instance(ctx)

    message = str(excinfo.value)
    assert str(project_a) in message
    assert str(elsewhere) in message
    # The refusal must not leave a half-applied selection behind.
    assert await middleware.get_active_instance(ctx) is None


@pytest.mark.asyncio
async def test_selects_sole_pluginhub_instance_when_cwd_is_inside_it(monkeypatch, tmp_path):
    project_a = tmp_path / "ProjectA"
    (project_a / "Assets").mkdir(parents=True)
    monkeypatch.chdir(project_a / "Assets")

    _single_session_plugin_hub(monkeypatch, project_path=str(project_a))
    from transport.unity_instance_middleware import UnityInstanceMiddleware

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    assert await middleware._maybe_autoselect_instance(ctx) == "ProjectA@aaaaaaaa"


@pytest.mark.asyncio
async def test_selects_sole_pluginhub_instance_when_cwd_contains_it(monkeypatch, tmp_path):
    """Monorepo launch: the client runs from a root that holds the Unity project."""
    repo = tmp_path / "repo"
    project_a = repo / "ProjectA"
    project_a.mkdir(parents=True)
    monkeypatch.chdir(repo)

    _single_session_plugin_hub(monkeypatch, project_path=str(project_a))
    from transport.unity_instance_middleware import UnityInstanceMiddleware

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    assert await middleware._maybe_autoselect_instance(ctx) == "ProjectA@aaaaaaaa"


@pytest.mark.asyncio
async def test_opt_out_restores_legacy_cross_project_autoselect(monkeypatch, tmp_path):
    project_a = tmp_path / "ProjectA"
    elsewhere = tmp_path / "ProjectB"
    project_a.mkdir()
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    _single_session_plugin_hub(monkeypatch, project_path=str(project_a))
    monkeypatch.setattr(config, "allow_cross_project_autoselect", True)
    from transport.unity_instance_middleware import UnityInstanceMiddleware

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    assert await middleware._maybe_autoselect_instance(ctx) == "ProjectA@aaaaaaaa"


@pytest.mark.asyncio
async def test_refuses_sole_stdio_instance_from_an_unrelated_project(monkeypatch, tmp_path):
    """The stdio discovery path reports the Assets folder; it needs the same guard."""
    project_a = tmp_path / "ProjectA"
    elsewhere = tmp_path / "ProjectB"
    (project_a / "Assets").mkdir(parents=True)
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    plugin_hub = types.ModuleType("transport.plugin_hub")

    class PluginHub:
        @classmethod
        def is_configured(cls) -> bool:
            return False

    plugin_hub.PluginHub = PluginHub
    monkeypatch.setitem(sys.modules, "transport.plugin_hub", plugin_hub)
    monkeypatch.delitem(
        sys.modules, "transport.unity_instance_middleware", raising=False)
    monkeypatch.setattr(config, "transport_mode", "stdio")
    monkeypatch.setattr(config, "allow_cross_project_autoselect", False)

    class PoolStub:
        def discover_all_instances(self, force_refresh=False):
            return [SimpleNamespace(
                id="ProjectA@cc8756d4", path=str(project_a / "Assets"))]

    unity_connection = types.ModuleType("transport.legacy.unity_connection")
    unity_connection.get_unity_connection_pool = lambda: PoolStub()
    monkeypatch.setitem(
        sys.modules, "transport.legacy.unity_connection", unity_connection)

    from transport.unity_instance_middleware import (
        UnityInstanceMiddleware,
        CrossProjectAutoSelectError,
    )

    middleware = UnityInstanceMiddleware()
    ctx = DummyContext()
    ctx.client_id = "client-1"

    with pytest.raises(CrossProjectAutoSelectError):
        await middleware._maybe_autoselect_instance(ctx)

    assert await middleware.get_active_instance(ctx) is None
