"""Input simulation CLI commands — mouse, keyboard, touch, and sequence actions."""

import json
import sys
from typing import Optional, Any

import click

from cli.utils.config import get_config
from cli.utils.output import format_output, print_error, print_success, print_info
from cli.utils.connection import run_command, handle_unity_errors
from cli.utils.parsers import parse_json_dict_or_exit


def _parse_target_or_exit(value: str, option_name: str) -> dict[str, Any]:
    """Parse a JSON string into a target dict, exiting with an error on failure."""
    return parse_json_dict_or_exit(value, option_name)


@click.group()
def input_simulation():
    """Input simulation — mouse, keyboard, touch, and sequence actions (Play Mode)."""
    pass


@input_simulation.command("discover")
@click.option("--filter", "-f", "filter_text", default=None, help="Substring filter for element names.")
@click.option("--page-size", default=None, type=int, help="Max results (default 50).")
@click.option("--cursor", default=None, type=int, help="Pagination cursor (0-based offset).")
@handle_unity_errors
def discover(filter_text: Optional[str], page_size: Optional[int], cursor: Optional[int]):
    """List interactive UI elements in the current scene.

    \b
    Examples:
        unity-mcp input-simulation discover
        unity-mcp input-simulation discover --filter "Button"
        unity-mcp input-simulation discover --page-size 20 --cursor 20
    """
    config = get_config()
    params: dict[str, Any] = {"action": "discover"}
    if filter_text:
        params["filter"] = filter_text
    if page_size is not None:
        params["page_size"] = page_size
    if cursor is not None:
        params["cursor"] = cursor
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))


@input_simulation.command("bounds")
@click.option("--target", "-t", required=True, help="Target as JSON, e.g. '{\"mode\":\"ui_element\",\"name\":\"StartButton\"}'.")
@handle_unity_errors
def bounds(target: str):
    """Get the bounding rect of a UI element.

    \b
    Examples:
        unity-mcp input-simulation bounds --target '{"mode":"ui_element","name":"StartButton"}'
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "get_element_bounds", "target": target_dict}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))


@input_simulation.command("click")
@click.option("--target", "-t", required=True, help="Target as JSON.")
@click.option("--button", "-b", default=None, type=click.Choice(["left", "right", "middle"]), help="Mouse button (default: left).")
@handle_unity_errors
def click_cmd(target: str, button: Optional[str]):
    """Click at a target position or element.

    \b
    Examples:
        unity-mcp input-simulation click --target '{"mode":"coordinates","x":100,"y":200}'
        unity-mcp input-simulation click --target '{"mode":"ui_element","name":"StartButton"}' --button left
        unity-mcp input-simulation click --target '{"mode":"gameobject","name":"Enemy"}' --button right
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "click", "target": target_dict}
    if button:
        params["button"] = button
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Click performed")


@input_simulation.command("double-click")
@click.option("--target", "-t", required=True, help="Target as JSON.")
@click.option("--button", "-b", default=None, type=click.Choice(["left", "right", "middle"]), help="Mouse button (default: left).")
@handle_unity_errors
def double_click(target: str, button: Optional[str]):
    """Double-click at a target position or element.

    \b
    Examples:
        unity-mcp input-simulation double-click --target '{"mode":"ui_element","name":"Icon"}'
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "double_click", "target": target_dict}
    if button:
        params["button"] = button
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Double-click performed")


@input_simulation.command("move")
@click.option("--target", "-t", required=True, help="Target as JSON.")
@handle_unity_errors
def move(target: str):
    """Move the mouse cursor to a target position.

    \b
    Examples:
        unity-mcp input-simulation move --target '{"mode":"coordinates","x":300,"y":400}'
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "mouse_move", "target": target_dict}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Mouse moved")


@input_simulation.command("drag")
@click.option("--from", "from_target", required=True, help="Source target as JSON.")
@click.option("--to", "to_target", required=True, help="Destination target as JSON.")
@click.option("--duration", "-d", default=None, type=int, help="Duration in ms (default: 300).")
@handle_unity_errors
def drag(from_target: str, to_target: str, duration: Optional[int]):
    """Drag from one target to another.

    \b
    Examples:
        unity-mcp input-simulation drag --from '{"mode":"coordinates","x":100,"y":100}' --to '{"mode":"coordinates","x":400,"y":400}'
        unity-mcp input-simulation drag --from '{"mode":"gameobject","name":"Card"}' --to '{"mode":"ui_element","name":"Slot"}' --duration 500
    """
    config = get_config()
    from_dict = _parse_target_or_exit(from_target, "from")
    to_dict = _parse_target_or_exit(to_target, "to")
    params: dict[str, Any] = {
        "action": "drag",
        "from_target": from_dict,
        "to_target": to_dict,
    }
    if duration is not None:
        params["duration_ms"] = duration
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Drag performed")


@input_simulation.command("scroll")
@click.option("--target", "-t", required=True, help="Target as JSON.")
@click.option("--dx", default=None, type=float, help="Horizontal scroll delta.")
@click.option("--dy", default=None, type=float, help="Vertical scroll delta.")
@handle_unity_errors
def scroll(target: str, dx: Optional[float], dy: Optional[float]):
    """Scroll the mouse wheel at a target position.

    \b
    Examples:
        unity-mcp input-simulation scroll --target '{"mode":"ui_element","name":"ScrollView"}' --dy -3.0
        unity-mcp input-simulation scroll --target '{"mode":"coordinates","x":500,"y":300}' --dy 2.0 --dx 1.0
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "scroll", "target": target_dict}
    if dx is not None:
        params["delta_x"] = dx
    if dy is not None:
        params["delta_y"] = dy
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Scroll performed")


@input_simulation.command("key")
@click.argument("key_name")
@click.option("--mode", "-m", default=None, type=click.Choice(["press", "release", "tap"]), help="Key mode (default: tap).")
@handle_unity_errors
def key(key_name: str, mode: Optional[str]):
    """Press, release, or tap a key.

    \b
    Examples:
        unity-mcp input-simulation key space
        unity-mcp input-simulation key escape --mode tap
        unity-mcp input-simulation key a --mode press
        unity-mcp input-simulation key a --mode release
    """
    config = get_config()
    params: dict[str, Any] = {"action": "key_press", "key": key_name}
    if mode:
        params["mode"] = mode
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success(f"Key '{key_name}' performed")


@input_simulation.command("combo")
@click.argument("key_name")
@click.option("--mod", "-m", "modifiers", multiple=True, type=click.Choice(["ctrl", "shift", "alt"]), help="Modifier key (can be repeated).")
@handle_unity_errors
def combo(key_name: str, modifiers: tuple):
    """Press a key combination with modifier keys.

    \b
    Examples:
        unity-mcp input-simulation combo z --mod ctrl
        unity-mcp input-simulation combo s --mod ctrl --mod shift
        unity-mcp input-simulation combo f4 --mod alt
    """
    config = get_config()
    params: dict[str, Any] = {"action": "key_combo", "key": key_name}
    if modifiers:
        params["modifiers"] = list(modifiers)
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        mods_str = "+".join(modifiers) + "+" if modifiers else ""
        print_success(f"Key combo '{mods_str}{key_name}' performed")


@input_simulation.command("text")
@click.argument("text_value")
@handle_unity_errors
def text(text_value: str):
    """Type a string of text into the focused input field.

    \b
    Examples:
        unity-mcp input-simulation text "Hello, World!"
        unity-mcp input-simulation text "player@example.com"
    """
    config = get_config()
    params: dict[str, Any] = {"action": "text_input", "text": text_value}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Text input performed")


@input_simulation.command("tap")
@click.option("--target", "-t", required=True, help="Target as JSON.")
@handle_unity_errors
def tap(target: str):
    """Perform a single touch tap at a target position.

    \b
    Examples:
        unity-mcp input-simulation tap --target '{"mode":"coordinates","x":200,"y":300}'
        unity-mcp input-simulation tap --target '{"mode":"ui_element","name":"PlayButton"}'
    """
    config = get_config()
    target_dict = _parse_target_or_exit(target, "target")
    params: dict[str, Any] = {"action": "touch_tap", "target": target_dict}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Touch tap performed")


@input_simulation.command("swipe")
@click.option("--from", "from_target", required=True, help="Source target as JSON.")
@click.option("--to", "to_target", required=True, help="Destination target as JSON.")
@click.option("--duration", "-d", default=None, type=int, help="Duration in ms (default: 300).")
@handle_unity_errors
def swipe(from_target: str, to_target: str, duration: Optional[int]):
    """Perform a touch swipe gesture from one target to another.

    \b
    Examples:
        unity-mcp input-simulation swipe --from '{"mode":"coordinates","x":300,"y":600}' --to '{"mode":"coordinates","x":300,"y":100}'
        unity-mcp input-simulation swipe --from '{"mode":"anchor","anchor":"bottom"}' --to '{"mode":"anchor","anchor":"top"}' --duration 400
    """
    config = get_config()
    from_dict = _parse_target_or_exit(from_target, "from")
    to_dict = _parse_target_or_exit(to_target, "to")
    params: dict[str, Any] = {
        "action": "touch_swipe",
        "from_target": from_dict,
        "to_target": to_dict,
    }
    if duration is not None:
        params["duration_ms"] = duration
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Touch swipe performed")


@input_simulation.command("pinch")
@click.option("--center", "-c", required=True, help="Center target as JSON.")
@click.option("--start-dist", required=True, type=float, help="Starting distance between fingers (px).")
@click.option("--end-dist", required=True, type=float, help="Ending distance between fingers (px).")
@click.option("--duration", "-d", default=None, type=int, help="Duration in ms (default: 300).")
@handle_unity_errors
def pinch(center: str, start_dist: float, end_dist: float, duration: Optional[int]):
    """Perform a two-finger pinch gesture.

    \b
    Examples:
        unity-mcp input-simulation pinch --center '{"mode":"coordinates","x":400,"y":300}' --start-dist 200 --end-dist 50
        unity-mcp input-simulation pinch --center '{"mode":"anchor","anchor":"center"}' --start-dist 50 --end-dist 200 --duration 600
    """
    config = get_config()
    center_dict = _parse_target_or_exit(center, "center")
    params: dict[str, Any] = {
        "action": "touch_pinch",
        "center_target": center_dict,
        "start_distance": start_dist,
        "end_distance": end_dist,
    }
    if duration is not None:
        params["duration_ms"] = duration
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        print_success("Touch pinch performed")


@input_simulation.command("sequence")
@click.argument("steps_json")
@handle_unity_errors
def sequence(steps_json: str):
    """Execute multiple input steps in order.

    STEPS_JSON is a JSON array of step objects, each with {action, params, delay_ms}.

    \b
    Examples:
        unity-mcp input-simulation sequence '[{"action":"click","params":{"target":{"mode":"ui_element","name":"StartButton"}},"delay_ms":200},{"action":"key_press","params":{"key":"space"},"delay_ms":100}]'
    """
    config = get_config()
    try:
        steps = json.loads(steps_json)
    except json.JSONDecodeError as e:
        print_error(f"Invalid JSON for steps: {e}")
        sys.exit(1)
    if not isinstance(steps, list):
        print_error("steps must be a JSON array")
        sys.exit(1)
    params: dict[str, Any] = {"action": "sequence", "steps": steps}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        job_id = result.get("data", {}).get("job_id") if isinstance(result.get("data"), dict) else None
        if job_id:
            print_info(f"Sequence job started: {job_id}")
            print_info(f"Poll with: unity-mcp input-simulation status {job_id}")
        else:
            print_success("Sequence completed")


@input_simulation.command("status")
@click.argument("job_id")
@handle_unity_errors
def status(job_id: str):
    """Poll an async sequence job for status and results.

    \b
    Examples:
        unity-mcp input-simulation status abc123
    """
    config = get_config()
    params: dict[str, Any] = {"action": "status", "job_id": job_id}
    result = run_command("manage_input_simulation", params, config)
    click.echo(format_output(result, config.format))
    if isinstance(result, dict) and result.get("success"):
        data = result.get("data", {})
        job_status = data.get("status", "unknown") if isinstance(data, dict) else "unknown"
        if job_status == "completed":
            print_success("Sequence completed")
        elif job_status == "running":
            print_info("Sequence still running")
        elif job_status == "failed":
            print_error("Sequence failed")
