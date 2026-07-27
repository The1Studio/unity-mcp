"""Defines the batch_execute tool for orchestrating multiple Unity MCP commands."""
from __future__ import annotations

import logging
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

logger = logging.getLogger(__name__)

# Fallback used when the Unity-side configured limit is not yet known.
DEFAULT_MAX_COMMANDS_PER_BATCH = 25

# Hard ceiling matching the C# AbsoluteMaxCommandsPerBatch.
ABSOLUTE_MAX_COMMANDS_PER_BATCH = 100

# Per-instance cache for the Unity-configured limit (populated from editor state).
# The limit is a Unity-side EditorPrefs value read through an instance-routed
# get_editor_state query, so it can differ between the Unity instances a single
# server process serves (HTTP transport is shared-process). A module-global
# scalar applied whichever instance was read FIRST to every instance thereafter,
# which wrongly rejects a valid batch on an instance with a higher limit.
_cached_max_commands: dict[str | None, int] = {}


async def _get_max_commands_from_editor_state(ctx: Context) -> int:
    """
    Attempt to read the configured batch limit from the Unity editor state.
    Falls back to DEFAULT_MAX_COMMANDS_PER_BATCH if unavailable.
    """
    instance = await get_unity_instance_from_context(ctx)
    cached = _cached_max_commands.get(instance)
    if cached is not None:
        return cached

    try:
        from services.resources.editor_state import get_editor_state

        state_resp = await get_editor_state(ctx)
        data = state_resp.data if hasattr(state_resp, "data") else (
            state_resp.get("data") if isinstance(state_resp, dict) else None
        )
        if isinstance(data, dict):
            settings = data.get("settings")
            if isinstance(settings, dict):
                limit = settings.get("batch_execute_max_commands")
                if isinstance(limit, int) and 1 <= limit <= ABSOLUTE_MAX_COMMANDS_PER_BATCH:
                    _cached_max_commands[instance] = limit
                    return limit
    except Exception as exc:
        logger.debug("Could not read batch limit from editor state: %s", exc)

    return DEFAULT_MAX_COMMANDS_PER_BATCH


def invalidate_cached_max_commands() -> None:
    """Reset the cached limit so the next call re-reads from editor state."""
    _cached_max_commands.clear()


@mcp_for_unity_tool(
    name="batch_execute",
    description=(
        "Executes multiple MCP commands in a single batch for dramatically better performance. "
        "STRONGLY RECOMMENDED when creating/modifying multiple objects, adding components to multiple targets, "
        "or performing any repetitive operations. Reduces latency and token costs by 10-100x compared to "
        "sequential tool calls. The max commands per batch is configurable in the Unity MCP Tools window "
        f"(default {DEFAULT_MAX_COMMANDS_PER_BATCH}, hard max {ABSOLUTE_MAX_COMMANDS_PER_BATCH}). "
        "Example: creating 5 cubes → use 1 batch_execute with 5 create commands instead of 5 separate calls."
    ),
    annotations=ToolAnnotations(
        title="Batch Execute",
        destructiveHint=True,
    ),
)
async def batch_execute(
    ctx: Context,
    commands: Annotated[list[dict[str, Any]], "List of commands with 'tool' and 'params' keys."],
    parallel: Annotated[bool | None,
                        "Attempt to run read-only commands in parallel"] = None,
    fail_fast: Annotated[bool | None,
                         "Stop processing after the first failure"] = None,
    max_parallelism: Annotated[int | None,
                               "Hint for the maximum number of parallel workers"] = None,
) -> dict[str, Any]:
    """Proxy the batch_execute tool to the Unity Editor transporter."""
    unity_instance = await get_unity_instance_from_context(ctx)

    if not isinstance(commands, list) or not commands:
        raise ValueError(
            "'commands' must be a non-empty list of command specifications")

    max_commands = await _get_max_commands_from_editor_state(ctx)
    if len(commands) > max_commands:
        raise ValueError(
            f"batch_execute supports up to {max_commands} commands (configured in Unity); received {len(commands)}"
        )

    normalized_commands: list[dict[str, Any]] = []
    for index, command in enumerate(commands):
        if not isinstance(command, dict):
            raise ValueError(
                f"Command at index {index} must be an object with 'tool' and 'params' keys")

        tool_name = command.get("tool")
        params = command.get("params", {})

        if not tool_name or not isinstance(tool_name, str):
            raise ValueError(
                f"Command at index {index} is missing a valid 'tool' name")

        if params is None:
            params = {}
        if not isinstance(params, dict):
            raise ValueError(
                f"Command '{tool_name}' must specify parameters as an object/dict")

        if "unity_instance" in params:
            raise ValueError(
                f"Command '{tool_name}' at index {index} contains 'unity_instance'. "
                "Per-command instance routing is not supported inside batch_execute. "
                "Set unity_instance on the outer batch_execute call to route the entire batch."
            )

        normalized_commands.append({
            "tool": tool_name,
            "params": params,
        })

    payload: dict[str, Any] = {
        "commands": normalized_commands,
    }

    if parallel is not None:
        payload["parallel"] = bool(parallel)
    if fail_fast is not None:
        payload["failFast"] = bool(fail_fast)
    if max_parallelism is not None:
        payload["maxParallelism"] = int(max_parallelism)

    # batch_execute applies non-idempotent mutations sequentially with no
    # completion registry, so a connection-level replay re-runs every command
    # that already succeeded (issue #18: a 4-command batch produced 16 nodes).
    # Opt out of the retry the same way refresh_unity does for its
    # reload-triggering commands (issue #577). A timeout now surfaces as a
    # visible error instead of silent scene duplication.
    return await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "batch_execute",
        payload,
        retry_on_reload=False,
    )
