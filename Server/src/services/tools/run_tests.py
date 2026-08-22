"""Async Unity Test Runner jobs: start + poll."""
from __future__ import annotations

import asyncio
import logging
import time
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations
from pydantic import BaseModel

from models import MCPResponse
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from services.tools.preflight import preflight
import transport.unity_transport as unity_transport
from transport.legacy.unity_connection import async_send_command_with_retry
from transport.plugin_hub import PluginHub
from utils.focus_nudge import nudge_unity_focus, should_nudge, reset_nudge_backoff

logger = logging.getLogger(__name__)

# Strong references to background fire-and-forget tasks to prevent premature GC.
_background_tasks: set[asyncio.Task] = set()


async def _get_unity_project_path(unity_instance: str | None) -> str | None:
    """Get the project root path for a Unity instance (for focus nudging).

    Args:
        unity_instance: Unity instance hash or "Name@hash" format or None

    Returns:
        Project root path (e.g., "/Users/name/project"), or falls back to project_name if path unavailable
    """
    if not unity_instance:
        return None

    try:
        registry = PluginHub._registry
        if not registry:
            return None

        # Parse Name@hash format if present (middleware stores instances as "Name@hash")
        target_hash = unity_instance
        if "@" in target_hash:
            _, _, target_hash = target_hash.rpartition("@")
        if not target_hash:
            return None

        # Get session by hash
        session_id = await registry.get_session_id_by_hash(target_hash)
        if not session_id:
            return None

        session = await registry.get_session(session_id)
        if not session:
            return None

    except Exception as e:
        # Re-raise cancellation errors so task cancellation propagates
        if isinstance(e, asyncio.CancelledError):
            raise
        logger.debug(f"Could not get Unity project path: {e}")
        return None
    else:
        # Return full path if available, otherwise fall back to project name
        if session.project_path:
            return session.project_path
        return session.project_name if session.project_name else None


class RunTestsSummary(BaseModel):
    total: int
    passed: int
    failed: int
    skipped: int
    durationSeconds: float
    resultState: str


class RunTestsTestResult(BaseModel):
    name: str
    fullName: str
    state: str
    durationSeconds: float
    message: str | None = None
    stackTrace: str | None = None
    output: str | None = None


class RunTestsResult(BaseModel):
    mode: str
    summary: RunTestsSummary
    results: list[RunTestsTestResult] | None = None


class RunTestsStartData(BaseModel):
    job_id: str
    status: str
    mode: str | None = None
    include_details: bool | None = None
    include_failed_tests: bool | None = None


class RunTestsStartResponse(MCPResponse):
    data: RunTestsStartData | None = None


class TestJobFailure(BaseModel):
    full_name: str | None = None
    message: str | None = None


class TestJobProgress(BaseModel):
    completed: int | None = None
    total: int | None = None
    current_test_full_name: str | None = None
    current_test_started_unix_ms: int | None = None
    last_finished_test_full_name: str | None = None
    last_finished_unix_ms: int | None = None
    stuck_suspected: bool | None = None
    editor_is_focused: bool | None = None
    blocked_reason: str | None = None
    failures_so_far: list[TestJobFailure] | None = None
    failures_capped: bool | None = None


class GetTestJobData(BaseModel):
    job_id: str
    status: str
    mode: str | None = None
    started_unix_ms: int | None = None
    finished_unix_ms: int | None = None
    last_update_unix_ms: int | None = None
    # True when run_tests was called with a test_names/group_names/category_names/assembly_names filter.
    filter_requested: bool | None = None
    # Number of tests Unity's Test Runner actually discovered/selected for this run (post-filter).
    discovered_tests: int | None = None
    progress: TestJobProgress | None = None
    error: str | None = None
    result: RunTestsResult | None = None


class GetTestJobResponse(MCPResponse):
    data: GetTestJobData | None = None


def _envelope_failure(response: Any) -> MCPResponse | None:
    """Validate a raw Unity transport response before it's splatted into a strict
    pydantic model (MCPResponse / RunTestsStartResponse / GetTestJobResponse all
    require "success" with no default).

    A non-dict response, or a dict missing "success" entirely (observed: Unity
    returning an empty {} for a run that had actually started -- see #63), used
    to sail past `.get("success", True)`'s permissive default and detonate as an
    uncaught pydantic ValidationError instead of a clean MCPResponse(success=False).

    Returns the MCPResponse to return immediately, or None when the envelope is
    well-formed and the caller should proceed with its own response handling.
    """
    if not isinstance(response, dict):
        return MCPResponse(success=False, error=str(response))
    if "success" not in response:
        return MCPResponse(
            success=False,
            error="empty_response_from_unity",
            message=(
                "Unity's response was missing or malformed. The underlying job may "
                "still be running (e.g. a busy/reload race) -- check for an "
                "in-progress job before retrying, rather than assuming it never started."
            ),
            data=response or None,
            hint="retry",
        )
    if not response["success"]:
        return MCPResponse(**response)
    return None


def _reject_zero_match_filter(response: dict[str, Any]) -> dict[str, Any]:
    """Turn a "Passed" result with 0 discovered tests into an explicit error.

    A test_names/group_names/category_names/assembly_names filter that matches no
    tests still reports resultState "Passed" (NUnit has nothing to fail). Silently
    returning that as success hides a caller bug (typo'd test name, stale filter) --
    see #37. Only rewrite terminal jobs that were actually filtered; a genuinely
    empty EditMode/PlayMode suite run without a filter is left alone.
    """
    if not isinstance(response, dict) or not response.get("success", True):
        return response

    data = response.get("data")
    if not isinstance(data, dict):
        return response

    if data.get("status") not in ("succeeded", "failed"):
        return response

    if not data.get("filter_requested"):
        return response

    # discovered_tests is None (not 0) when the run never got past initialization --
    # e.g. TestJobManager's init-timeout auto-fail, which sets data["status"] == "failed"
    # without ever setting TotalTests. Collapsing that None to 0 here would misreport a
    # genuine startup timeout as "filter matched 0 tests", masking the real cause.
    discovered_tests = data.get("discovered_tests")
    if discovered_tests is None or discovered_tests != 0:
        return response

    summary = ((data.get("result") or {}).get("summary")) or {}
    if summary.get("total", 0) != 0:
        return response

    return {
        "success": False,
        "error": (
            "Test filter matched 0 tests. test_names/group_names/category_names/"
            "assembly_names did not match any test Unity discovered for this run -- "
            "check for typos or a stale filter."
        ),
        "data": data,
    }


@mcp_for_unity_tool(
    group="testing",
    description="Starts a Unity test run asynchronously and returns a job_id immediately. Poll with get_test_job for progress.",
    annotations=ToolAnnotations(
        title="Run Tests",
        destructiveHint=True,
    ),
)
async def run_tests(
    ctx: Context,
    mode: Annotated[Literal["EditMode", "PlayMode"],
                    "Unity test mode to run"] = "EditMode",
    test_names: Annotated[list[str] | str,
                          "Full names of specific tests to run"] | None = None,
    group_names: Annotated[list[str] | str,
                           "Same as test_names, except it allows for Regex"] | None = None,
    category_names: Annotated[list[str] | str,
                              "NUnit category names to filter by"] | None = None,
    assembly_names: Annotated[list[str] | str,
                              "Assembly names to filter tests by"] | None = None,
    include_failed_tests: Annotated[bool,
                                    "Include details for failed/skipped tests only (default: false)"] = False,
    include_details: Annotated[bool,
                               "Include details for all tests (default: false)"] = False,
    init_timeout: Annotated[int | None,
                            "Initialization timeout in milliseconds. PlayMode tests may need longer "
                            "due to domain reload (default: 15000). Recommended: 120000 for PlayMode."] = None,
) -> RunTestsStartResponse | MCPResponse:
    if init_timeout is not None and init_timeout <= 0:
        return MCPResponse(success=False, error="init_timeout must be a positive integer (milliseconds) or None")

    unity_instance = await get_unity_instance_from_context(ctx)

    gate = await preflight(ctx, requires_no_tests=True, wait_for_no_compile=True, refresh_if_dirty=True)
    if isinstance(gate, MCPResponse):
        return gate

    def _coerce_string_list(value) -> list[str] | None:
        if value is None:
            return None
        if isinstance(value, str):
            return [value] if value.strip() else None
        if isinstance(value, list):
            result = [str(v).strip() for v in value if v and str(v).strip()]
            return result if result else None
        return None

    params: dict[str, Any] = {"mode": mode}
    if (t := _coerce_string_list(test_names)):
        params["testNames"] = t
    if (g := _coerce_string_list(group_names)):
        params["groupNames"] = g
    if (c := _coerce_string_list(category_names)):
        params["categoryNames"] = c
    if (a := _coerce_string_list(assembly_names)):
        params["assemblyNames"] = a
    if include_failed_tests:
        params["includeFailedTests"] = True
    if include_details:
        params["includeDetails"] = True
    if init_timeout is not None and init_timeout > 0:
        params["initTimeout"] = init_timeout

    response = await unity_transport.send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "run_tests",
        params,
    )

    if (guard := _envelope_failure(response)) is not None:
        return guard

    # response["success"] is True and well-formed past this point, but the data
    # payload can still be present-and-empty (e.g. {"success": true} with no
    # "data"). get_test_job hard-requires job_id with no recovery path, so catch
    # that here rather than let RunTestsStartResponse(**response) either raise on
    # the missing required field or silently construct a job the caller can never
    # poll for.
    data = response.get("data")
    if not isinstance(data, dict) or not data.get("job_id"):
        return MCPResponse(
            success=False,
            error="Unity returned an incomplete run_tests response (missing job_id); "
                  "the test run may not have started. Check the Unity console.",
            data=response or None,
        )
    return RunTestsStartResponse(**response)


@mcp_for_unity_tool(
    group="testing",
    description="Polls an async Unity test job by job_id.",
    annotations=ToolAnnotations(
        title="Get Test Job",
        readOnlyHint=True,
    ),
)
async def get_test_job(
    ctx: Context,
    job_id: Annotated[str, "Job id returned by run_tests"],
    include_failed_tests: Annotated[bool,
                                    "Include details for failed/skipped tests only (default: false)"] = False,
    include_details: Annotated[bool,
                               "Include details for all tests (default: false)"] = False,
    wait_timeout: Annotated[int | None,
                            "If set, wait up to this many seconds for tests to complete before returning. "
                            "Reduces polling frequency and avoids client-side loop detection. "
                            "Recommended: 30-60 seconds. Returns immediately if tests complete sooner."] = None,
) -> GetTestJobResponse | MCPResponse:
    unity_instance = await get_unity_instance_from_context(ctx)

    params: dict[str, Any] = {"job_id": job_id}
    if include_failed_tests:
        params["includeFailedTests"] = True
    if include_details:
        params["includeDetails"] = True

    async def _fetch_status() -> dict[str, Any]:
        return await unity_transport.send_with_unity_instance(
            async_send_command_with_retry,
            unity_instance,
            "get_test_job",
            params,
        )

    # If wait_timeout is specified, poll server-side until complete or timeout
    if wait_timeout and wait_timeout > 0:
        deadline = asyncio.get_event_loop().time() + wait_timeout
        poll_interval = 2.0  # Poll Unity every 2 seconds
        prev_last_update_unix_ms = None

        # Get project path once for focus nudging (multi-instance support)
        project_path = await _get_unity_project_path(unity_instance)

        while True:
            response = await _fetch_status()

            if (guard := _envelope_failure(response)) is not None:
                return guard

            # Check if tests are done
            data = response.get("data", {})
            status = data.get("status", "")
            if status in ("succeeded", "failed", "cancelled"):
                response = _reject_zero_match_filter(response)
                if not response.get("success", True):
                    return MCPResponse(**response)
                return GetTestJobResponse(**response)

            # Detect progress and reset exponential backoff
            last_update_unix_ms = data.get("last_update_unix_ms")
            if prev_last_update_unix_ms is not None and last_update_unix_ms != prev_last_update_unix_ms:
                # Progress detected - reset exponential backoff for next potential stall
                reset_nudge_backoff()
                logger.debug(f"Test job {job_id} made progress - reset nudge backoff")
            prev_last_update_unix_ms = last_update_unix_ms

            # Check if Unity needs a focus nudge to make progress
            # This handles OS-level throttling (e.g., macOS App Nap) that can
            # stall PlayMode tests when Unity is in the background.
            # Uses exponential backoff: 1s, 2s, 4s, 8s, 10s max between nudges.
            progress = data.get("progress") or {}
            editor_is_focused = progress.get("editor_is_focused", True)
            current_time_ms = int(time.time() * 1000)

            if should_nudge(
                status=status,
                editor_is_focused=editor_is_focused,
                last_update_unix_ms=last_update_unix_ms,
                current_time_ms=current_time_ms,
                # Use default stall_threshold_ms (3s)
            ):
                logger.info(f"Test job {job_id} appears stalled (unfocused Unity), attempting nudge...")
                # Lazily resolve project path if not yet available (registry may have become ready)
                if project_path is None:
                    project_path = await _get_unity_project_path(unity_instance)
                # Pass project path for multi-instance support
                nudged = await nudge_unity_focus(unity_project_path=project_path)
                if nudged:
                    logger.info(f"Test job {job_id} nudge completed")

            # Check timeout
            remaining = deadline - asyncio.get_event_loop().time()
            if remaining <= 0:
                # Timeout reached, return current status
                return GetTestJobResponse(**response)

            # Wait before next poll (but don't exceed remaining time)
            await asyncio.sleep(min(poll_interval, remaining))
    
    # No wait_timeout - return immediately (original behavior)
    response = await _fetch_status()
    if (guard := _envelope_failure(response)) is not None:
        return guard

    # Fire-and-forget nudge check: even without wait_timeout, clients may poll
    # externally. Check if Unity needs a nudge on every call so stalls get
    # detected regardless of polling style.
    data = response.get("data", {})
    status = data.get("status", "")
    if status == "running":
        progress = data.get("progress") or {}
        editor_is_focused = progress.get("editor_is_focused", True)
        last_update_unix_ms = data.get("last_update_unix_ms")
        current_time_ms = int(time.time() * 1000)
        if should_nudge(
            status=status,
            editor_is_focused=editor_is_focused,
            last_update_unix_ms=last_update_unix_ms,
            current_time_ms=current_time_ms,
        ):
            logger.info(f"Test job {job_id} appears stalled (unfocused Unity), scheduling background nudge...")
            project_path = await _get_unity_project_path(unity_instance)
            task = asyncio.create_task(nudge_unity_focus(unity_project_path=project_path))
            _background_tasks.add(task)
            task.add_done_callback(_background_tasks.discard)

    response = _reject_zero_match_filter(response)
    if not response.get("success", True):
        return MCPResponse(**response)
    return GetTestJobResponse(**response)
