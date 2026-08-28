"""
Tests for asset-preview integrity verification (issue #36).

The producing side reports the raw PNG's byte length and SHA-256 next to the base64.
These tests pin what happens when the arriving payload disagrees with those numbers —
including the case where it agrees, which is the one that must stay silent.
"""
import base64
import hashlib

import pytest

from services.tools.preview_integrity import verify_preview_payloads


PNG_BYTES = b"\x89PNG\r\n\x1a\n" + b"fake-idat-payload" * 4


def _record(**overrides):
    record = {
        "path": "Assets/Demos/RedBoss_Tinted.mat",
        "guid": "0123456789abcdef0123456789abcdef",
        "previewBase64": base64.b64encode(PNG_BYTES).decode("ascii"),
        "previewByteLength": len(PNG_BYTES),
        "previewSha256": hashlib.sha256(PNG_BYTES).hexdigest(),
        "previewWidth": 128,
        "previewHeight": 128,
    }
    record.update(overrides)
    return record


def test_intact_payload_reports_no_error_and_is_left_alone():
    record = _record()
    original = record["previewBase64"]

    assert verify_preview_payloads({"asset": record}) == []
    assert record["previewBase64"] == original
    assert "previewIntegrityError" not in record


def test_truncated_payload_is_rejected_and_stripped():
    truncated = PNG_BYTES[:10]
    record = _record(previewBase64=base64.b64encode(truncated).decode("ascii"))

    errors = verify_preview_payloads({"asset": record})

    assert len(errors) == 1
    assert "truncated in transit" in errors[0]
    assert "decoded 10 bytes" in errors[0]
    assert record["path"] in errors[0]
    assert record["previewBase64"] is None, "A corrupt payload must not be handed back."
    assert "previewIntegrityError" in record


def test_altered_payload_of_the_same_length_is_rejected():
    """The failure the byte-length check alone cannot see: same size, different bytes."""
    altered = bytearray(PNG_BYTES)
    altered[12] ^= 0xFF
    record = _record(previewBase64=base64.b64encode(bytes(altered)).decode("ascii"))

    errors = verify_preview_payloads({"asset": record})

    assert len(errors) == 1
    assert "altered in transit" in errors[0]
    assert record["previewBase64"] is None


def test_undecodable_payload_is_rejected():
    record = _record(previewBase64="not!valid!base64!")

    errors = verify_preview_payloads({"asset": record})

    assert len(errors) == 1
    assert "not valid base64" in errors[0]
    assert record["previewBase64"] is None


def test_record_without_integrity_fields_is_left_alone():
    """An editor package predating the integrity fields must not be flagged."""
    record = _record()
    del record["previewByteLength"]
    del record["previewSha256"]
    original = record["previewBase64"]

    assert verify_preview_payloads({"asset": record}) == []
    assert record["previewBase64"] == original


def test_record_without_a_preview_is_left_alone():
    record = {"path": "Assets/Foo.mat", "previewBase64": None, "previewByteLength": 0}
    assert verify_preview_payloads({"asset": record}) == []


def test_walks_nested_lists_of_records():
    good = _record(path="Assets/Good.mat")
    bad = _record(path="Assets/Bad.mat", previewBase64=base64.b64encode(b"short").decode("ascii"))

    errors = verify_preview_payloads({"assets": [good, {"nested": [bad]}]})

    assert len(errors) == 1
    assert "Assets/Bad.mat" in errors[0]
    assert good["previewBase64"] is not None
    assert bad["previewBase64"] is None


def test_sha_comparison_is_case_insensitive():
    record = _record(previewSha256=hashlib.sha256(PNG_BYTES).hexdigest().upper())
    assert verify_preview_payloads({"asset": record}) == []
