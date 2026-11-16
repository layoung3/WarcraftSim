"""Offline reliability contracts. Every HTTP response is synthetic and mocked."""
import argparse
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import urllib.error

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import acquisition_cache as cache
import cache_forever_talent_icons as talents
import download_client_csv as csv_tool
import review_forever_cache_icons as items


def tiny_jpeg():
    # Original synthetic 1x1 gray baseline image, no third-party artwork.
    def segment(marker, body):
        return bytes((255, marker)) + (len(body) + 2).to_bytes(2, "big") + body
    quant = segment(0xDB, bytes((0,)) + bytes((16,)) * 64)
    frame = segment(0xC0, bytes((8, 0, 1, 0, 1, 1, 1, 0x11, 0)))
    tables = segment(0xC4, bytes((0, 1)) + bytes(15) + bytes((0, 0x10, 1)) + bytes(16))
    scan = segment(0xDA, bytes((1, 1, 0, 0, 63, 0)))
    return b"\xff\xd8" + quant + frame + tables + scan + b"\x3f\xff\xd9"


class Response(io.BytesIO):
    def __init__(self, data=b"ID,Name\n1,One\n", status=200, headers=None):
        super().__init__(data)
        self.status = status
        self.headers = headers or {}


def http_error(code, headers=None):
    return urllib.error.HTTPError("https://example.test/data", code, "Mock error", headers or {}, None)


class AcquisitionReliabilityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "ItemSparse.csv"
        self.source = csv_tool.provenance("ItemSparse", "1.60.1.70707", "wow_classic")
        self.data = b"ID,Name\n1,One\n"
        self.network = patch("urllib.request.urlopen", side_effect=AssertionError("Unmocked networking prohibited"))
        self.network.start()
        self.addCleanup(self.network.stop)

    def acquire(self, **kwargs):
        return cache.acquire(self.path, self.source, csv_tool.validate_csv, 1024, **kwargs)

    def seed(self, data=None, headers=None):
        return self.acquire(opener=Mock(return_value=Response(data or self.data, headers=headers)))

    def test_first_download_receipt_and_offline_reuse(self):
        data, receipt, status = self.seed()
        self.assertEqual(status, "downloaded")
        self.assertEqual(receipt["sha256"], cache.digest(data))
        self.assertEqual(receipt["provenance"], self.source)
        self.assertEqual(self.acquire()[2], "cached")

    def test_corrupt_cached_bytes_do_not_repin(self):
        self.seed()
        original = cache.receipt_path(self.path).read_bytes()
        self.path.write_bytes(b"ID,Name\n1,Altered\n")
        with self.assertRaisesRegex(ValueError, "checksum"):
            self.acquire(refresh=True)
        self.assertEqual(cache.receipt_path(self.path).read_bytes(), original)

    def test_truncated_cache_is_rejected(self):
        self.seed()
        self.path.write_bytes(self.data[:7])
        with self.assertRaisesRegex(ValueError, "checksum"):
            self.acquire()

    def test_missing_or_unreceipted_cache_fails_closed(self):
        self.path.write_bytes(self.data)
        with self.assertRaisesRegex(ValueError, "Unreceipted"):
            self.acquire()
        self.assertFalse(cache.receipt_path(self.path).exists())

    def test_legacy_revalidation_requires_exact_fresh_bytes(self):
        self.path.write_bytes(self.data)
        self.assertEqual(self.acquire(revalidate_legacy=True,
            opener=Mock(return_value=Response(self.data)))[2], "revalidated")
        self.assertEqual(self.acquire()[2], "cached")

    def test_legacy_changed_bytes_preserved_without_receipt(self):
        self.path.write_bytes(self.data)
        with self.assertRaisesRegex(ValueError, "changed bytes"):
            self.acquire(revalidate_legacy=True, opener=Mock(return_value=Response(b"ID,Name\n2,Two\n")))
        self.assertEqual(self.path.read_bytes(), self.data)
        self.assertFalse(cache.receipt_path(self.path).exists())

    def test_refresh_changed_bytes_preserves_original_receipt(self):
        self.seed()
        original = cache.receipt_path(self.path).read_bytes()
        with self.assertRaisesRegex(ValueError, "changed bytes"):
            self.acquire(refresh=True, opener=Mock(return_value=Response(b"ID,Name\n2,Two\n")))
        self.assertEqual(self.path.read_bytes(), self.data)
        self.assertEqual(cache.receipt_path(self.path).read_bytes(), original)

    def test_refresh_same_bytes_keeps_original_receipt(self):
        self.seed()
        original = cache.receipt_path(self.path).read_bytes()
        self.assertEqual(self.acquire(refresh=True, opener=Mock(return_value=Response(self.data)))[2], "unchanged")
        self.assertEqual(cache.receipt_path(self.path).read_bytes(), original)

    def test_304_requires_verified_cached_bytes(self):
        with self.assertRaisesRegex(ValueError, "304 without"):
            self.acquire(opener=Mock(side_effect=http_error(304)))
        self.seed(headers={"ETag": '"first"'})
        opener = Mock(side_effect=http_error(304))
        self.assertEqual(self.acquire(refresh=True, opener=opener)[2], "not-modified")
        self.assertEqual(opener.call_args.args[0].get_header("If-none-match"), '"first"')

    def test_missing_cached_file_with_receipt_does_not_use_304(self):
        self.seed()
        self.path.unlink()
        with self.assertRaisesRegex(ValueError, "missing cache"):
            self.acquire(refresh=True)

    def test_304_does_not_bless_unreceipted_legacy(self):
        self.path.write_bytes(self.data)
        with self.assertRaisesRegex(ValueError, "304 without"):
            self.acquire(revalidate_legacy=True, opener=Mock(return_value=Response(status=304)))

    def test_source_build_product_and_url_mismatch_are_rejected(self):
        self.seed()
        for field in ("requestedBuild", "requestedProduct", "url"):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "mismatch"):
                cache.validate_cached(self.path, {**self.source, field: "different"}, csv_tool.validate_csv, 1024)

    def test_oversized_response_has_no_published_cache(self):
        with self.assertRaisesRegex(ValueError, "byte limit"):
            self.acquire(opener=Mock(return_value=Response(b"x" * 1025)))
        self.assertFalse(self.path.exists())
        self.assertFalse(cache.receipt_path(self.path).exists())
        self.assertFalse(list(self.path.parent.glob("*.partial")))

    def test_oversized_content_length_rejected_before_read(self):
        response = Response(headers={"Content-Length": "1025"})
        response.read = Mock(side_effect=AssertionError("Must not read oversized body"))
        with self.assertRaisesRegex(ValueError, "Content-Length"):
            self.acquire(opener=Mock(return_value=response))

    def test_truncated_network_body_rejected(self):
        with self.assertRaisesRegex(ValueError, "Truncated"):
            self.acquire(opener=Mock(return_value=Response(headers={"Content-Length": "80"})))

    def test_no_store_or_no_cache_response_never_gets_receipt(self):
        for value in ("public, no-store", "no-cache"):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "Cache-Control"):
                self.acquire(opener=Mock(return_value=Response(headers={"Cache-Control": value})))
            self.assertFalse(self.path.exists())
            self.assertFalse(cache.receipt_path(self.path).exists())

    def test_304_no_store_preserves_but_does_not_reuse_cached_evidence(self):
        self.seed()
        before = cache.receipt_path(self.path).read_bytes()
        with self.assertRaisesRegex(ValueError, "Cache-Control"):
            self.acquire(refresh=True, opener=Mock(side_effect=http_error(304, {"Cache-Control": "no-store"})))
        self.assertEqual(cache.receipt_path(self.path).read_bytes(), before)

    def test_receipt_publication_failure_leaves_untrusted_file_not_partial_success(self):
        real_write = cache.atomic_write
        def fail_receipt(path, data):
            if str(path).endswith(".receipt.json"):
                raise OSError("Mock failed receipt publication")
            return real_write(path, data)
        with patch.object(cache, "atomic_write", side_effect=fail_receipt):
            with self.assertRaises(OSError):
                self.seed()
        self.assertEqual(self.path.read_bytes(), self.data)
        self.assertFalse(cache.receipt_path(self.path).exists())
        with self.assertRaisesRegex(ValueError, "Unreceipted"):
            self.acquire()

    def test_429_503_and_530_retry_after_respected(self):
        for code in (429, 503, 530):
            opener = Mock(side_effect=[http_error(code, {"Retry-After": "5"}), Response()])
            sleep = Mock()
            with self.subTest(code=code):
                cache.request_bytes(self.source["url"], 1024, opener=opener, sleep=sleep)
                self.assertEqual(opener.call_count, 2)
                sleep.assert_called_once_with(5)

    def test_long_retry_after_fails_without_retrying_early(self):
        opener = Mock(side_effect=http_error(530, {"Retry-After": "120", "Cache-Control": "no-store"}))
        sleep = Mock()
        with self.assertRaisesRegex(ValueError, "retry budget"):
            self.acquire(opener=opener, sleep=sleep)
        self.assertEqual(opener.call_count, 1)
        sleep.assert_not_called()
        self.assertFalse(self.path.exists())

    def test_retry_exhaustion_is_finite(self):
        opener = Mock(side_effect=http_error(429))
        sleep = Mock()
        with self.assertRaises(urllib.error.HTTPError):
            self.acquire(opener=opener, sleep=sleep, attempts=3)
        self.assertEqual(opener.call_count, 3)
        self.assertEqual([x.args[0] for x in sleep.call_args_list], [1, 2])

    def test_nonretryable_error_is_not_retried(self):
        opener = Mock(side_effect=http_error(404))
        sleep = Mock()
        with self.assertRaises(urllib.error.HTTPError):
            self.acquire(opener=opener, sleep=sleep)
        self.assertEqual(opener.call_count, 1)
        sleep.assert_not_called()

    def test_timeouts_have_finite_retry_budget(self):
        opener = Mock(side_effect=TimeoutError("Mock timeout"))
        with self.assertRaises(TimeoutError):
            self.acquire(opener=opener, sleep=Mock(), attempts=2)
        self.assertEqual(opener.call_count, 2)

    def test_invalid_bounds_are_rejected(self):
        for kwargs in ({"timeout": float("inf")}, {"timeout": -1}, {"attempts": 0}, {"attempts": 100}):
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                self.acquire(**kwargs)

    def test_http_date_retry_after(self):
        self.assertEqual(cache.retry_delay("Thu, 01 Jan 1970 00:00:10 GMT", 0, now=0), 10)

    def test_atomic_write_failure_preserves_previous_file_and_cleans_temp(self):
        self.path.write_bytes(self.data)
        with patch.object(cache.os, "replace", side_effect=OSError("Mock publication failure")):
            with self.assertRaises(OSError):
                cache.atomic_write(self.path, b"replacement")
        self.assertEqual(self.path.read_bytes(), self.data)
        self.assertFalse(list(self.path.parent.glob("*.partial")))

    def test_existing_lock_is_not_removed_or_bypassed(self):
        lock = Path(str(self.path) + ".lock")
        lock.write_text("other writer")
        with self.assertRaises(FileExistsError):
            self.acquire()
        self.assertEqual(lock.read_text(), "other writer")

    def test_report_history_is_retained(self):
        old = {"build": "1.60.1.70707", "tables": {"legacy": "receipt"}}
        cache.atomic_write(self.path.parent / "acquisition-report.json", cache.json_bytes(old))
        csv_tool.write_report(self.path.parent, {"tables": {}})
        archive = self.path.parent / "acquisition-reports" / (cache.digest(cache.json_bytes(old)) + ".json")
        self.assertEqual(json.loads(archive.read_bytes()), old)

    def test_archived_legacy_evidence_cannot_be_silently_repinned(self):
        out = self.path.parent
        self.path.write_bytes(self.data)
        old = {"build": "1.60.1.70707", "product": "wow_classic", "tables": {
            "ItemSparse": {"url": self.source["url"], "sha256": "historical-different-bytes", "bytes": len(self.data)}}}
        csv_tool.write_report(out, old)
        csv_tool.write_report(out, {"tables": {}, "failures": {"ItemSparse": "unreceipted"}})
        with self.assertRaisesRegex(ValueError, "Legacy acquisition evidence mismatch"):
            csv_tool.check_legacy_evidence(out, "ItemSparse", self.source)

    def test_matching_legacy_evidence_allows_fresh_revalidation(self):
        self.path.write_bytes(self.data)
        csv_tool.write_report(self.path.parent, {"build": "1.60.1.70707", "product": "wow_classic", "tables": {
            "ItemSparse": {"url": self.source["url"], "sha256": cache.digest(self.data), "bytes": len(self.data)}}})
        csv_tool.check_legacy_evidence(self.path.parent, "ItemSparse", self.source)
        self.assertFalse(cache.receipt_path(self.path).exists())

    def test_csv_format_rejects_html_duplicate_headers_and_partial_row(self):
        for data in (b"<html>,error</html>", b"ID,ID\n1,2\n", b"ID,Name\n1,One,extra\n", b"ID,Name\n1,\"unfinished"):
            with self.subTest(data=data), self.assertRaises((ValueError, csv_tool.csv.Error)):
                csv_tool.validate_csv(data)

    def test_fetch_reports_requested_product_and_reuses_without_network(self):
        args = argparse.Namespace(build="1.60.1.70707", product="wow_classic", directory=self.temp.name,
                                  tables=["ItemSparse"], force=False)
        with patch("urllib.request.urlopen", return_value=Response()), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(csv_tool.fetch(args), 0)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(csv_tool.fetch(args), 0)
        report = json.loads((Path(self.temp.name) / args.build / "acquisition-report.json").read_bytes())
        self.assertEqual(report["product"], "wow_classic")
        self.assertEqual(report["tables"]["ItemSparse"]["status"], "cached")
        self.assertIn("not-attested", report["buildProductAttestation"])

    def test_manual_metadata_staging_remains_explicitly_unverified(self):
        folder = Path(self.temp.name) / "1.60.1.70707"
        folder.mkdir()
        (folder / "ItemSparse.csv").write_bytes(self.data)
        output = Path(self.temp.name) / "metadata.json"
        args = argparse.Namespace(build=folder.name, directory=self.temp.name, output=output,
                                  imported_at="2026-11-09T00:00:00Z")
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(csv_tool.stage(args), 0)
        manifest = json.loads(output.read_bytes())
        self.assertEqual(manifest["acquisitionStatus"], "unreceipted-local-input-not-source-verified")
        self.assertEqual(manifest["items"], [])
        self.assertEqual(manifest["sourceSha256"], cache.digest(self.data))

    def test_staging_rejects_wrong_build_receipt_before_writing(self):
        folder = Path(self.temp.name) / "1.60.1.70708"
        folder.mkdir()
        self.seed()
        (folder / "ItemSparse.csv").write_bytes(self.path.read_bytes())
        cache.receipt_path(folder / "ItemSparse.csv").write_bytes(cache.receipt_path(self.path).read_bytes())
        output = Path(self.temp.name) / "metadata.json"
        args = argparse.Namespace(build=folder.name, directory=self.temp.name, output=output,
                                  imported_at="2026-11-09T00:00:00Z")
        with self.assertRaisesRegex(ValueError, "mismatch"):
            csv_tool.stage(args)
        self.assertFalse(output.exists())

    def test_valid_jpeg_and_truncated_corrupt_or_oversized_frames(self):
        data = tiny_jpeg()
        cache.validate_jpeg(data)
        invalid = [data[:-2], b"\xff\xd8" + b"junk" * 30 + b"\xff\xd9", data + b"trailer",
                   data.replace(bytes((8, 0, 1, 0, 1)), bytes((8, 0, 1, 8, 0)))]
        for payload in invalid:
            with self.subTest(payload=payload[:12]), self.assertRaises(ValueError):
                cache.validate_jpeg(payload)

    def test_talent_and_item_writers_validate_receipts(self):
        for module, name in ((talents, "spell_test"), (items, "inv_test")):
            writer = (lambda: talents.download(name, Path(self.temp.name), 1)) if module is talents else (
                lambda: items.cache_icon(name, Path(self.temp.name)))
            with patch("urllib.request.urlopen", return_value=Response(tiny_jpeg())):
                self.assertEqual(writer(), "downloaded")
            self.assertEqual(writer(), "cached")
            image = Path(self.temp.name) / (name + ".jpg")
            original = cache.receipt_path(image).read_bytes()
            image.write_bytes(tiny_jpeg()[:-2])
            with self.assertRaisesRegex(ValueError, "checksum"):
                writer()
            self.assertEqual(cache.receipt_path(image).read_bytes(), original)

    def test_icon_source_version_mismatch_not_reused(self):
        with patch("urllib.request.urlopen", return_value=Response(tiny_jpeg())):
            talents.download("spell_test", Path(self.temp.name), 1)
        with patch.object(talents, "SOURCE_COMMIT", "different"):
            with self.assertRaisesRegex(ValueError, "mismatch"):
                talents.download("spell_test", Path(self.temp.name), 1)

    def test_item_metadata_checks_existing_reviewed_source_hash(self):
        reviewed = Path(__file__).resolve().parents[2] / "WarcraftSim.Api/Catalog/forever-equipment-icons-cp174-provisional.json"
        self.assertEqual(items.CACHE_SHA256, json.loads(reviewed.read_bytes())["cacheSha256"])
        with self.assertRaisesRegex(ValueError, "checksum mismatch"):
            items.validate_source_cache(b"[]")

    def test_unsafe_icon_names_rejected_before_network_or_path_access(self):
        for key in ("../escape", "UPPER", "icon.jpg", "a/b"):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "Unsafe"):
                talents.download(key, Path(self.temp.name), 1)

    def test_existing_only_does_not_acquire_missing_talent_art(self):
        manifest = Path(self.temp.name) / "manifest.json"
        manifest.write_text(json.dumps({"schema": "warcraftsim-forever-talent-reference-v1",
                                       "sourceCommit": talents.SOURCE_COMMIT,
                                       "records": [{"iconKey": "spell_missing"}]}))
        with patch.object(sys, "argv", ["cache", "--manifest", str(manifest), "--dest", self.temp.name,
                                        "--existing-only", "--revalidate-legacy"]), patch.object(talents, "download") as download:
            with contextlib.redirect_stdout(io.StringIO()):
                talents.main()
            download.assert_not_called()


if __name__ == "__main__":
    unittest.main()
