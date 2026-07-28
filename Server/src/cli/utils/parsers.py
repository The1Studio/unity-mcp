"""JSON and value parsing utilities for CLI commands."""
import json
import sys
from typing import Any

import click

from cli.utils.output import print_error, print_info


def parse_float_list(value: str, name: str = "value") -> list[float]:
    """Parse a comma-separated ``"x,y,z"`` string into a list of floats.

    Raises ``click.BadParameter`` (a clean CLI error + non-zero exit) instead of
    letting a malformed component (trailing comma, empty part, non-number) crash
    the command with an uncaught ``ValueError`` traceback. Mirrors the guarded
    pattern in ``prefab._parse_vector3``.
    """
    try:
        return [float(p.strip()) for p in value.split(",")]
    except ValueError as e:
        raise click.BadParameter(
            f"{name} must be comma-separated numbers, got: '{value}'"
        ) from e


def parse_int_list(value: str, name: str = "value") -> list[int]:
    """Parse a comma-separated ``"1,2,3"`` string into a list of ints.

    Like :func:`parse_float_list`, raises ``click.BadParameter`` on a malformed
    component instead of an uncaught ``ValueError``.
    """
    try:
        return [int(p.strip()) for p in value.split(",")]
    except ValueError as e:
        raise click.BadParameter(
            f"{name} must be comma-separated integers, got: '{value}'"
        ) from e


def parse_value_safe(value: str) -> Any:
    """Parse a value, trying JSON → float → string fallback.

    This is used for property values that could be JSON objects/arrays,
    numbers, or strings. Never raises an exception.

    Args:
        value: The string value to parse

    Returns:
        Parsed JSON object/array, float, or original string

    Examples:
        >>> parse_value_safe('{"x": 1}')
        {'x': 1}
        >>> parse_value_safe('3.14')
        3.14
        >>> parse_value_safe('hello')
        'hello'
    """
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        # Try to parse as number
        try:
            return float(value)
        except ValueError:
            # Keep as string
            return value


def parse_json_or_exit(value: str, context: str = "parameter") -> Any:
    """Parse JSON string, trying to fix common issues, or exit with error.

    Attempts to parse JSON with automatic fixes for:
    - Single quotes instead of double quotes
    - Python-style True/False instead of true/false

    Args:
        value: The JSON string to parse
        context: Description of what's being parsed (for error messages)

    Returns:
        Parsed JSON object

    Exits:
        Calls sys.exit(1) if JSON is invalid after attempting fixes
    """
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        # Try to fix common shell quoting issues (single quotes, Python bools)
        try:
            fixed = value.replace("'", '"').replace("True", "true").replace("False", "false")
            return json.loads(fixed)
        except json.JSONDecodeError as e:
            print_error(f"Invalid JSON for {context}: {e}")
            print_info("Example: --params '{\"key\":\"value\"}'")
            print_info("Tip: wrap JSON in single quotes to avoid shell escaping issues.")
            sys.exit(1)


def parse_json_dict_or_exit(value: str, context: str = "parameter") -> dict[str, Any]:
    """Parse JSON object (dict), or exit with error.

    Like parse_json_or_exit, but ensures result is a dictionary.

    Args:
        value: The JSON string to parse
        context: Description of what's being parsed (for error messages)

    Returns:
        Parsed JSON object as dictionary

    Exits:
        Calls sys.exit(1) if JSON is invalid or not an object
    """
    result = parse_json_or_exit(value, context)
    if not isinstance(result, dict):
        print_error(f"Invalid JSON for {context}: expected an object, got {type(result).__name__}")
        sys.exit(1)
    return result


def parse_json_list_or_exit(value: str, context: str = "parameter") -> list[Any]:
    """Parse JSON array (list), or exit with error.

    Like parse_json_or_exit, but ensures result is a list.

    Args:
        value: The JSON string to parse
        context: Description of what's being parsed (for error messages)

    Returns:
        Parsed JSON array as list

    Exits:
        Calls sys.exit(1) if JSON is invalid or not an array
    """
    result = parse_json_or_exit(value, context)
    if not isinstance(result, list):
        print_error(f"Invalid JSON for {context}: expected an array, got {type(result).__name__}")
        sys.exit(1)
    return result
