"""Bounded opt-in acquisition helpers; immutable receipts are not source approval.

No runtime networking, licensing claims, schema inference, or automatic repairs.
An invalid/missing receipt, changed source, or changed bytes requires review in a
separate destination. Existing evidence is never overwritten by a refresh.
"""
import contextlib
import datetime
import email.utils
import hashlib
import http.client
import json
import math
import os
from pathlib import Path
import tempfile
import time
import urllib.error
import urllib.request

SCHEMA = "warcraftsim-acquisition-receipt-v1"
MAX_RECEIPT_BYTES = 16_384


def digest(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(document):
    return (json.dumps(document, indent=2, sort_keys=True) + "\n").encode("utf-8")


def atomic_write(path, data):
    """Publish complete bytes only; clean up only this call's temporary file."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=path.name + ".", suffix=".partial", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def bounded_file(path, limit):
    with Path(path).open("rb") as stream:
        data = stream.read(limit + 1)
    if not data or len(data) > limit:
        raise ValueError("Local file is empty or exceeds byte limit")
    return data


def retry_delay(value, attempt, now=None):
    """Honor Retry-After within a fixed budget; never retry earlier than asked."""
    if value:
        try:
            delay = float(value)
        except ValueError:
            try:
                when = email.utils.parsedate_to_datetime(value)
                delay = when.timestamp() - (time.time() if now is None else now)
            except (TypeError, ValueError, OverflowError):
                delay = 2 ** attempt
        if not math.isfinite(delay):
            raise ValueError("Invalid Retry-After")
        if delay > 30:
            raise ValueError("Retry-After exceeds 30-second retry budget; retry later")
        return max(0, delay)
    return min(2 ** attempt, 30)


def require_offline_cacheable(headers):
    lowered = {key.lower(): value for key, value in headers.items()}
    controls = {part.strip().split("=", 1)[0].lower()
                for part in lowered.get("cache-control", "").split(",")}
    if controls.intersection(("no-store", "no-cache")):
        raise ValueError("Source Cache-Control forbids persistent offline reuse")


def request_bytes(url, limit, *, timeout=30, attempts=3, headers=None, opener=None, sleep=None):
    """At most four requests, bounded reads, finite waits, no opaque compression."""
    if not url.startswith("https://"):
        raise ValueError("Only HTTPS sources are supported")
    if not isinstance(limit, int) or not 0 < limit <= 128_000_000:
        raise ValueError("Invalid response byte limit")
    if not math.isfinite(timeout) or not 0 < timeout <= 90:
        raise ValueError("Timeout must be finite and between 0 and 90 seconds")
    if not isinstance(attempts, int) or not 1 <= attempts <= 4:
        raise ValueError("Attempts must be between 1 and 4")
    opener = opener or urllib.request.urlopen
    sleep = sleep or time.sleep
    request = urllib.request.Request(url, headers={
        "User-Agent": "WarcraftSim-local-acquisition/2.0", "Accept-Encoding": "identity", **(headers or {})})
    for attempt in range(attempts):
        retry_after = None
        try:
            with opener(request, timeout=timeout) as response:
                status = response.status
                response_headers = dict(response.headers.items())
                require_offline_cacheable(response_headers)
                if status == 304:
                    return None, response_headers
                if status != 200:
                    raise ValueError(f"Unexpected HTTP status {status}")
                if response.headers.get("Content-Encoding", "identity").lower() not in ("", "identity"):
                    raise ValueError("Unexpected compressed response")
                length = response.headers.get("Content-Length")
                expected = None if length is None else int(length)
                if expected is not None and not 0 < expected <= limit:
                    raise ValueError("Response Content-Length exceeds byte bounds")
                data = response.read(limit + 1)
                if not data or len(data) > limit:
                    raise ValueError("Response is empty or exceeds byte limit")
                if expected is not None and len(data) != expected:
                    raise ValueError("Truncated response: Content-Length mismatch")
                return data, response_headers
        except urllib.error.HTTPError as error:
            if error.code == 304:
                headers304 = dict(error.headers.items())
                error.close()
                require_offline_cacheable(headers304)
                return None, headers304
            retry_after = error.headers.get("Retry-After") if error.headers else None
            retryable = error.code in (429, 500, 502, 503, 504, 520, 521, 522, 523, 524, 525, 526, 527, 530)
            error.close()
            if not retryable or attempt + 1 == attempts:
                raise
        except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.IncompleteRead):
            if attempt + 1 == attempts:
                raise
        sleep(retry_delay(retry_after, attempt))
    raise AssertionError("Unreachable retry state")


def receipt_path(path):
    return Path(str(path) + ".receipt.json")


def validate_cached(path, provenance, validator, limit):
    """Every reuse checks the receipt, original identity, byte hash and format."""
    path = Path(path)
    receipt = json.loads(bounded_file(receipt_path(path), MAX_RECEIPT_BYTES))
    if not isinstance(receipt, dict) or receipt.get("schema") != SCHEMA:
        raise ValueError("Unsupported acquisition receipt")
    if receipt.get("provenance") != provenance:
        raise ValueError("Cache source/version/build/product mismatch; use a separate reviewed destination")
    data = bounded_file(path, limit)
    if receipt.get("bytes") != len(data) or receipt.get("sha256") != digest(data):
        raise ValueError("Cache checksum or size mismatch; original receipt preserved")
    validator(data)
    return data, receipt


@contextlib.contextmanager
def target_lock(path):
    """Concurrent/stale writers fail closed rather than clobbering evidence."""
    lock = Path(str(path) + ".lock")
    with lock.open("x", encoding="ascii") as handle:
        handle.write(str(os.getpid()))
    try:
        yield
    finally:
        lock.unlink()


def acquire(path, provenance, validator, limit, *, refresh=False, revalidate_legacy=False, timeout=30,
            attempts=3, headers=None, opener=None, sleep=None):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with target_lock(path):
        old_data, old_receipt = None, None
        if path.exists() or receipt_path(path).exists():
            if path.is_file() and not receipt_path(path).exists() and revalidate_legacy:
                old_data = bounded_file(path, limit)
                validator(old_data)
            else:
                if not path.is_file() or not receipt_path(path).is_file():
                    raise ValueError("Unreceipted or missing cache file; preserve it and explicitly revalidate or use a separate reviewed destination")
                old_data, old_receipt = validate_cached(path, provenance, validator, limit)
                if not refresh:
                    return old_data, old_receipt, "cached"
        request_headers = dict(headers or {})
        if old_receipt:
            for field, header in (("etag", "If-None-Match"), ("lastModified", "If-Modified-Since")):
                if old_receipt.get(field):
                    request_headers[header] = old_receipt[field]
        data, response_headers = request_bytes(provenance["url"], limit, timeout=timeout,
            attempts=attempts, headers=request_headers, opener=opener, sleep=sleep)
        lowered = {key.lower(): value for key, value in response_headers.items()}
        require_offline_cacheable(response_headers)
        if data is None:
            if old_receipt is None:
                raise ValueError("HTTP 304 without a verified local cache")
            return old_data, old_receipt, "not-modified"
        validator(data)
        if old_data is not None:
            if data != old_data:
                raise ValueError("Pinned source returned changed bytes; original data and receipt preserved")
            if old_receipt:
                return old_data, old_receipt, "unchanged"
        receipt = {"schema": SCHEMA, "provenance": provenance, "sha256": digest(data),
                   "bytes": len(data), "acquiredAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                   "etag": lowered.get("etag"), "lastModified": lowered.get("last-modified"),
                   "validation": "jpeg-structure-v1" if validator is validate_jpeg else "source-format-v1"}
        # A crash between writes leaves unreceipted bytes that cannot be reused.
        if old_data is None:
            atomic_write(path, data)
        atomic_write(receipt_path(path), json_bytes(receipt))
        return data, receipt, "revalidated" if old_data is not None else "downloaded"


def validate_jpeg(data):
    """Validate bounded JPEG marker structure, dimensions and completed scans.

    This is structural validation, not a codec or an assertion of art licensing.
    Later corruption is additionally detected by the immutable byte checksum.
    """
    if not data.startswith(b"\xff\xd8") or not data.endswith(b"\xff\xd9"):
        raise ValueError("Invalid or truncated JPEG")
    pos, frame, scan, quantization, huffman = 2, False, False, False, False
    while pos < len(data):
        if data[pos] != 0xFF:
            raise ValueError("Invalid JPEG marker")
        while pos < len(data) and data[pos] == 0xFF:
            pos += 1
        if pos == len(data):
            break
        marker = data[pos]
        pos += 1
        if marker == 0xD9:
            if pos != len(data) or not (frame and scan and quantization and huffman):
                raise ValueError("Incomplete JPEG structure")
            return
        if marker in (0, 0xD8) or 0xD0 <= marker <= 0xD7:
            raise ValueError("Unexpected JPEG marker")
        if pos + 2 > len(data):
            break
        length = int.from_bytes(data[pos:pos + 2], "big")
        if length < 2 or pos + length > len(data) - 2:
            raise ValueError("Truncated JPEG segment")
        segment = data[pos + 2:pos + length]
        pos += length
        if marker in (0xC0, 0xC1, 0xC2):
            if len(segment) < 6:
                raise ValueError("Invalid JPEG frame")
            height, width = int.from_bytes(segment[1:3], "big"), int.from_bytes(segment[3:5], "big")
            components = segment[5]
            if (segment[0] != 8 or not 0 < width <= 1024 or not 0 < height <= 1024 or
                    components not in (1, 3) or len(segment) != 6 + 3 * components):
                raise ValueError("Invalid or oversized JPEG dimensions/components")
            frame = True
        elif marker == 0xDB:
            offset = 0
            while offset < len(segment):
                precision, table = segment[offset] >> 4, segment[offset] & 15
                size = 64 * (precision + 1)
                if precision not in (0, 1) or table > 3 or offset + 1 + size > len(segment):
                    raise ValueError("Invalid JPEG quantization table")
                values = segment[offset + 1:offset + 1 + size]
                if any(int.from_bytes(values[i:i + precision + 1], "big") == 0
                       for i in range(0, size, precision + 1)):
                    raise ValueError("Invalid zero JPEG quantization value")
                offset += size + 1
            if not offset:
                raise ValueError("Empty JPEG quantization table")
            quantization = True
        elif marker == 0xC4:
            offset = 0
            while offset < len(segment):
                if offset + 17 > len(segment):
                    raise ValueError("Truncated JPEG Huffman table")
                kind, table = segment[offset] >> 4, segment[offset] & 15
                counts = segment[offset + 1:offset + 17]
                symbols = sum(counts)
                slots = 1
                for count in counts:
                    slots = slots * 2 - count
                    if slots < 0:
                        raise ValueError("Oversubscribed JPEG Huffman table")
                if kind not in (0, 1) or table > 3 or not 0 < symbols <= 256 or offset + 17 + symbols > len(segment):
                    raise ValueError("Invalid JPEG Huffman table")
                offset += 17 + symbols
            if not offset:
                raise ValueError("Empty JPEG Huffman table")
            huffman = True
        elif marker == 0xDA:
            if not frame or len(segment) < 6 or len(segment) != 4 + 2 * segment[0]:
                raise ValueError("Invalid JPEG scan")
            scan_start = pos
            while pos < len(data) - 1:
                if data[pos] != 0xFF:
                    pos += 1
                elif data[pos + 1] == 0 or 0xD0 <= data[pos + 1] <= 0xD7:
                    pos += 2
                else:
                    break
            if pos == scan_start:
                raise ValueError("Empty JPEG scan")
            scan = True
    raise ValueError("Incomplete JPEG")
