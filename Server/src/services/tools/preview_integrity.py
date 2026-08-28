"""
Integrity verification for asset preview payloads.

manage_asset's generate_preview path encodes a PNG in C#, base64s it, and ships it
across the Unity bridge. Issue #36 reports previews arriving decodable-but-corrupt
(garbled IDAT). Two layers could be responsible — the C# render-texture readback, or
the base64/JSON transport — and nothing measured either, so the question stayed open.

The editor now reports the raw PNG's byte length and SHA-256 alongside the base64.
Comparing those against what actually arrived settles it in one shot:

- digests match  -> the bytes crossed the wire intact; any corruption is in the C#
  encode (render-texture format / colour space), not the transport.
- digests differ -> the transport mangled or truncated the payload.

Either way a payload that fails the check is removed rather than handed back, so a
consumer gets an explicit error instead of a corrupt image.
"""
import base64
import hashlib
from typing import Any

PREVIEW_KEY = "previewBase64"
LENGTH_KEY = "previewByteLength"
SHA_KEY = "previewSha256"
ERROR_KEY = "previewIntegrityError"


def _check_record(record: dict[str, Any]) -> str | None:
    """Verifies one asset record in place. Returns an error string, or None if fine."""
    encoded = record.get(PREVIEW_KEY)
    if not encoded or not isinstance(encoded, str):
        return None

    expected_length = record.get(LENGTH_KEY)
    expected_sha = record.get(SHA_KEY)

    # An editor package predating the integrity fields reports neither. Nothing to
    # verify, and inventing a failure there would be worse than staying quiet.
    if expected_length is None and expected_sha is None:
        return None

    try:
        decoded = base64.b64decode(encoded, validate=True)
    except Exception as e:
        error = f"previewBase64 is not valid base64 ({e})."
        record[PREVIEW_KEY] = None
        record[ERROR_KEY] = error
        return error

    if isinstance(expected_length, int) and len(decoded) != expected_length:
        error = (
            f"preview payload truncated in transit: decoded {len(decoded)} bytes, "
            f"editor encoded {expected_length}."
        )
        record[PREVIEW_KEY] = None
        record[ERROR_KEY] = error
        return error

    if isinstance(expected_sha, str) and expected_sha:
        actual_sha = hashlib.sha256(decoded).hexdigest()
        if actual_sha.lower() != expected_sha.lower():
            error = (
                "preview payload altered in transit: decoded bytes hash to "
                f"{actual_sha}, editor reported {expected_sha}."
            )
            record[PREVIEW_KEY] = None
            record[ERROR_KEY] = error
            return error

    return None


def verify_preview_payloads(payload: Any) -> list[str]:
    """
    Walks a manage_asset response, verifying every asset record carrying a preview.
    Corrupt payloads are stripped in place. Returns every error found, newest last.
    """
    errors: list[str] = []

    def walk(node: Any) -> None:
        if isinstance(node, dict):
            if PREVIEW_KEY in node:
                error = _check_record(node)
                if error:
                    path = node.get("path") or node.get("guid") or "<unknown asset>"
                    errors.append(f"{path}: {error}")
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for item in node:
                walk(item)

    walk(payload)
    return errors
