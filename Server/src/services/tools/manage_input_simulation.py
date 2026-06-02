"""
Play Mode input simulation tool — mouse, keyboard, touch, and sequence actions.
Actions: discover, get_element_bounds, click, double_click, mouse_move, drag, scroll,
key_press, key_combo, text_input, touch_tap, touch_swipe, touch_pinch, sequence, status.
"""
from __future__ import annotations

from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Play Mode input simulation. "
        "Actions: discover (list interactive UI elements), "
        "get_element_bounds (bounding rect of a UI element), "
        "click (left/right/middle mouse click), "
        "double_click (double mouse click), "
        "mouse_move (move cursor to position), "
        "drag (click-hold-drag from one target to another), "
        "scroll (mouse wheel scroll), "
        "key_press (press/release/tap a key), "
        "key_combo (key with modifier keys, e.g. Ctrl+Z), "
        "text_input (type a string of text), "
        "touch_tap (single touch tap), "
        "touch_swipe (touch swipe gesture), "
        "touch_pinch (two-finger pinch gesture), "
        "sequence (execute multiple input steps in order), "
        "status (poll async sequence job by job_id)."
    ),
    group="testing",
    annotations=ToolAnnotations(title="Manage Input Simulation", destructiveHint=True),
)
async def manage_input_simulation(
    ctx: Context,
    action: Annotated[
        Literal[
            "discover",
            "get_element_bounds",
            "click",
            "double_click",
            "mouse_move",
            "drag",
            "scroll",
            "key_press",
            "key_combo",
            "text_input",
            "touch_tap",
            "touch_swipe",
            "touch_pinch",
            "sequence",
            "status",
        ],
        "Action to perform",
    ],
    target: Annotated[
        dict | None,
        "Target: {mode:'coordinates'|'gameobject'|'ui_element'|'anchor', ...}",
    ] = None,
    from_target: Annotated[dict | None, "Source target for drag/swipe"] = None,
    to_target: Annotated[dict | None, "Destination target for drag/swipe"] = None,
    center_target: Annotated[dict | None, "Center target for pinch"] = None,
    button: Annotated[str | None, "Mouse button: left, right, middle"] = None,
    key: Annotated[str | None, "Key name (e.g. space, a, escape)"] = None,
    modifiers: Annotated[list[str] | None, "Modifier keys (ctrl, shift, alt)"] = None,
    mode: Annotated[str | None, "Key mode: press, release, tap"] = None,
    text: Annotated[str | None, "Text to type"] = None,
    delta_x: Annotated[float | None, "Scroll delta X"] = None,
    delta_y: Annotated[float | None, "Scroll delta Y"] = None,
    duration_ms: Annotated[int | None, "Duration in ms for drag/swipe/pinch"] = None,
    start_distance: Annotated[float | None, "Start distance for pinch (px)"] = None,
    end_distance: Annotated[float | None, "End distance for pinch (px)"] = None,
    steps: Annotated[
        list[dict] | None, "Sequence steps [{action, params, delay_ms}]"
    ] = None,
    job_id: Annotated[str | None, "Job ID for status polling"] = None,
    flush: Annotated[bool | None, "Flush input immediately (default true)"] = None,
    capture_after: Annotated[
        bool | None, "Take screenshot after action (default false)"
    ] = None,
    filter: Annotated[str | None, "Filter for discover (substring match)"] = None,
    page_size: Annotated[int | None, "Max results (default 50)"] = None,
    cursor: Annotated[int | None, "Pagination cursor"] = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params: dict[str, Any] = {
        "action": action,
        "target": target,
        "from_target": from_target,
        "to_target": to_target,
        "center_target": center_target,
        "button": button,
        "key": key,
        "modifiers": modifiers,
        "mode": mode,
        "text": text,
        "delta_x": delta_x,
        "delta_y": delta_y,
        "duration_ms": duration_ms,
        "start_distance": start_distance,
        "end_distance": end_distance,
        "steps": steps,
        "job_id": job_id,
        "flush": flush,
        "capture_after": capture_after,
        "filter": filter,
        "page_size": page_size,
        "cursor": cursor,
    }
    # Strip None values — only send what the caller explicitly provided
    params = {k: v for k, v in params.items() if v is not None}

    response = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "manage_input_simulation", params
    )
    return response
