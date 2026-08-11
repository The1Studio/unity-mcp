import pytest

from .test_helpers import DummyContext


@pytest.mark.asyncio
async def test_run_tests_async_forwards_params(monkeypatch):
    from services.tools.run_tests import run_tests

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        captured["command_type"] = command_type
        captured["params"] = params
        return {"success": True, "data": {"job_id": "abc123", "status": "running", "mode": "EditMode"}}

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await run_tests(
        DummyContext(),
        mode="EditMode",
        test_names="MyNamespace.MyTests.TestA",
        include_details=True,
    )
    assert captured["command_type"] == "run_tests"
    assert captured["params"]["mode"] == "EditMode"
    assert captured["params"]["testNames"] == ["MyNamespace.MyTests.TestA"]
    assert captured["params"]["includeDetails"] is True
    assert resp.success is True
    assert resp.data is not None
    assert resp.data.job_id == "abc123"


@pytest.mark.asyncio
async def test_run_tests_forwards_init_timeout(monkeypatch):
    from services.tools.run_tests import run_tests

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        captured["params"] = params
        return {"success": True, "data": {"job_id": "abc123", "status": "running", "mode": "PlayMode"}}

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await run_tests(
        DummyContext(),
        mode="PlayMode",
        init_timeout=120000,
    )
    assert captured["params"]["initTimeout"] == 120000
    assert resp.success is True


@pytest.mark.asyncio
async def test_run_tests_omits_init_timeout_when_none(monkeypatch):
    from services.tools.run_tests import run_tests

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        captured["params"] = params
        return {"success": True, "data": {"job_id": "abc123", "status": "running", "mode": "EditMode"}}

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await run_tests(DummyContext(), mode="EditMode")
    assert "initTimeout" not in captured["params"]
    assert resp.success is True


@pytest.mark.asyncio
async def test_run_tests_rejects_negative_init_timeout():
    from services.tools.run_tests import run_tests

    resp = await run_tests(DummyContext(), mode="EditMode", init_timeout=-1)
    assert resp.success is False
    assert "init_timeout" in resp.error


@pytest.mark.asyncio
async def test_run_tests_rejects_zero_init_timeout():
    from services.tools.run_tests import run_tests

    resp = await run_tests(DummyContext(), mode="EditMode", init_timeout=0)
    assert resp.success is False
    assert "init_timeout" in resp.error


@pytest.mark.asyncio
async def test_get_test_job_forwards_job_id(monkeypatch):
    from services.tools.run_tests import get_test_job

    captured = {}

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        captured["command_type"] = command_type
        captured["params"] = params
        return {"success": True, "data": {"job_id": params["job_id"], "status": "running", "mode": "EditMode"}}

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await get_test_job(DummyContext(), job_id="job-1")
    assert captured["command_type"] == "get_test_job"
    assert captured["params"]["job_id"] == "job-1"
    assert resp.success is True
    assert resp.data is not None
    assert resp.data.job_id == "job-1"


def _zero_match_job_response(status="succeeded"):
    return {
        "success": True,
        "data": {
            "job_id": "job-zero",
            "status": status,
            "mode": "EditMode",
            "filter_requested": True,
            "discovered_tests": 0,
            "progress": {"completed": 0, "total": 0},
            "result": {
                "mode": "EditMode",
                "summary": {
                    "total": 0,
                    "passed": 0,
                    "failed": 0,
                    "skipped": 0,
                    "durationSeconds": 0.01,
                    "resultState": "Passed",
                },
            },
        },
    }


@pytest.mark.asyncio
async def test_get_test_job_rejects_zero_match_filter(monkeypatch):
    """#37: a test_names/group_names/... filter that matches nothing must not report Passed."""
    from services.tools.run_tests import get_test_job

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        return _zero_match_job_response()

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await get_test_job(DummyContext(), job_id="job-zero")
    assert resp.success is False
    assert "0 tests" in resp.error
    assert resp.data["discovered_tests"] == 0


@pytest.mark.asyncio
async def test_get_test_job_rejects_zero_match_filter_via_wait_timeout(monkeypatch):
    """Same guard applies on the server-side polling path (wait_timeout)."""
    from services.tools.run_tests import get_test_job

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        return _zero_match_job_response()

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await get_test_job(DummyContext(), job_id="job-zero", wait_timeout=5)
    assert resp.success is False
    assert "0 tests" in resp.error


@pytest.mark.asyncio
async def test_get_test_job_allows_zero_match_when_no_filter_requested(monkeypatch):
    """A genuinely empty suite (no filter applied) must still report success normally."""
    from services.tools.run_tests import get_test_job

    payload = _zero_match_job_response()
    payload["data"]["filter_requested"] = False

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        return payload

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await get_test_job(DummyContext(), job_id="job-zero")
    assert resp.success is True
    assert resp.data.discovered_tests == 0


@pytest.mark.asyncio
async def test_get_test_job_allows_filter_with_matched_tests(monkeypatch):
    """A filter that matched real tests must not be affected by the zero-match guard."""
    from services.tools.run_tests import get_test_job

    payload = _zero_match_job_response()
    payload["data"]["discovered_tests"] = 3
    payload["data"]["result"]["summary"]["total"] = 3
    payload["data"]["result"]["summary"]["passed"] = 3

    async def fake_send_with_unity_instance(send_fn, unity_instance, command_type, params, **kwargs):
        return payload

    import services.tools.run_tests as mod
    monkeypatch.setattr(
        mod.unity_transport, "send_with_unity_instance", fake_send_with_unity_instance)

    resp = await get_test_job(DummyContext(), job_id="job-zero")
    assert resp.success is True
    assert resp.data.discovered_tests == 3
