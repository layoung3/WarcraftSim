"""Offline source-selection regressions; HTTP and artwork bytes are synthetic."""
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import acquisition_cache as cache
import cache_forever_talent_icons as icons


def tiny_jpeg():
    def segment(marker, body):
        return bytes((255, marker)) + (len(body) + 2).to_bytes(2, "big") + body
    return (b"\xff\xd8"
            + segment(0xDB, bytes((0,)) + bytes((16,)) * 64)
            + segment(0xC0, bytes((8, 0, 1, 0, 1, 1, 1, 0x11, 0)))
            + segment(0xC4, bytes((0, 1)) + bytes(15) + bytes((0, 0x10, 1)) + bytes(16))
            + segment(0xDA, bytes((1, 1, 0, 0, 63, 0))) + b"\x3f\xff\xd9")


class PaladinIconSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.key = "inv_sword_07"
        self.path = self.folder / (self.key + ".jpg")
        self.receipt_path = cache.receipt_path(self.path)
        self.item_source = {
            "url": icons.ITEM_SOURCE_URL.format(key=self.key),
            "sourceVersion": icons.ITEM_SOURCE_COMMIT,
        }
        self.data = tiny_jpeg()
        self.network = patch.object(cache, "request_bytes",
                                    side_effect=AssertionError("Unexpected network acquisition"))
        self.request = self.network.start()
        self.addCleanup(self.network.stop)

    def manifest(self, records):
        path = self.folder / "manifest.json"
        path.write_text(json.dumps({"schema": "warcraftsim-forever-talent-reference-v1",
                                    "records": records}), encoding="utf-8")
        return path

    def seed(self, source):
        with patch.object(cache, "request_bytes", return_value=(self.data, {})) as fetch:
            self.assertEqual(icons.download(self.key, self.folder, 8, source=source), "downloaded")
            fetch.assert_called_once()
        return self.path.read_bytes(), self.receipt_path.read_bytes()

    def assert_cache_unchanged(self, before):
        self.assertEqual((self.path.read_bytes(), self.receipt_path.read_bytes()), before)
        self.request.assert_not_called()

    def test_default_download_and_manifest_contract_stays_on_original_pin(self):
        path = self.manifest([{"iconKey": self.key}, {"iconKey": self.key}])
        self.assertEqual(icons.icon_keys(path), [self.key])
        source = icons.icon_sources(path)[self.key]
        self.assertEqual(source, {"url": icons.SOURCE_URL.format(key=self.key),
                                  "sourceVersion": icons.SOURCE_COMMIT})
        before = self.seed(None)
        self.assertEqual(json.loads(before[1])["provenance"]["url"], source["url"])
        self.assertEqual(icons.download(self.key, self.folder, 8), "cached")
        self.assert_cache_unchanged(before)

    def test_explicit_item_pin_downloads_then_reuses_immutable_receipt(self):
        before = self.seed(self.item_source)
        receipt = json.loads(before[1])
        self.assertEqual(receipt["provenance"], {
            **self.item_source, "iconKey": self.key, "kind": "icon",
            "sourceStatus": "historical-reference-not-current-build-verified",
        })
        self.assertEqual(icons.download(self.key, self.folder, 8, source=self.item_source), "cached")
        self.assert_cache_unchanged(before)

    def test_paladin_manifest_has_only_reviewed_sword_exception(self):
        path = Path(__file__).resolve().parents[2] / "WarcraftSim.Api/Catalog/forever-paladin-artwork-v1.json"
        sources = icons.icon_sources(path)
        self.assertEqual(len(sources), 49)
        self.assertEqual(sources[self.key], self.item_source)
        self.assertEqual([key for key, source in sources.items()
                          if source["sourceVersion"] != icons.SOURCE_COMMIT], [self.key])
        document = json.loads(path.read_text(encoding="utf-8"))
        sword = next(record for record in document["records"] if record["iconKey"] == self.key)
        self.assertEqual(sword["artworkBlobSha"], "a0304e14e79f8f899b8ad15265d7309ab306fd2b")

    def test_cli_uses_selected_source_and_keeps_original_cache_pair(self):
        before = self.seed(self.item_source)
        path = self.manifest([{"iconKey": self.key, "artworkSource": self.item_source}])
        with patch.object(sys, "argv", ["cache-icons", "--manifest", str(path), "--dest", str(self.folder)]), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            icons.main()
        self.assertIn("'cached': 1, 'failed': 0", output.getvalue())
        self.assert_cache_unchanged(before)

    def test_default_cannot_relabel_item_receipt_even_with_refresh_or_legacy_option(self):
        before = self.seed(self.item_source)
        for options in ({}, {"refresh": True}, {"revalidate_legacy": True}):
            with self.subTest(options=options), self.assertRaisesRegex(ValueError, "mismatch"):
                icons.download(self.key, self.folder, 8, **options)
            self.assert_cache_unchanged(before)

    def test_item_pin_cannot_relabel_default_receipt(self):
        before = self.seed(None)
        with self.assertRaisesRegex(ValueError, "mismatch"):
            icons.download(self.key, self.folder, 8, source=self.item_source)
        self.assert_cache_unchanged(before)

    def test_corrupted_cached_bytes_are_rejected_without_overwrite(self):
        self.seed(self.item_source)
        self.path.write_bytes(self.data[:-1])
        before = self.path.read_bytes(), self.receipt_path.read_bytes()
        with self.assertRaisesRegex(ValueError, "checksum"):
            icons.download(self.key, self.folder, 8, source=self.item_source, refresh=True)
        self.assert_cache_unchanged(before)

    def test_foreign_receipt_is_rejected_without_repair(self):
        self.seed(self.item_source)
        original = json.loads(self.receipt_path.read_bytes())
        for field, value in (("url", "https://example.test/inv_sword_07.jpg"),
                             ("sourceVersion", "main"), ("iconKey", "inv_sword_08")):
            with self.subTest(field=field):
                receipt = {**original, "provenance": {**original["provenance"], field: value}}
                self.receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
                before = self.path.read_bytes(), self.receipt_path.read_bytes()
                with self.assertRaisesRegex(ValueError, "mismatch"):
                    icons.download(self.key, self.folder, 8, source=self.item_source, refresh=True)
                self.assert_cache_unchanged(before)

    def test_refresh_changed_bytes_preserves_original_data_and_receipt(self):
        before = self.seed(self.item_source)
        changed = self.data[:-3] + b"\x1f\xff\xd9"
        with patch.object(cache, "request_bytes", return_value=(changed, {})), \
                self.assertRaisesRegex(ValueError, "changed bytes"):
            icons.download(self.key, self.folder, 8, source=self.item_source, refresh=True)
        self.assert_cache_unchanged(before)

    def test_unknown_sources_fail_before_acquisition(self):
        for source in (None, "napalmsteak", [], {},
                       {**self.item_source, "sourceVersion": "main"},
                       {**self.item_source, "sourceVersion": icons.SOURCE_COMMIT},
                       {**self.item_source, "url": "https://example.test/inv_sword_07.jpg"},
                       {**self.item_source, "extra": "unreviewed"}):
            with self.subTest(source=source):
                path = self.manifest([{"iconKey": self.key, "artworkSource": source}])
                with self.assertRaisesRegex(ValueError, "Unknown"):
                    icons.icon_sources(path)
        self.request.assert_not_called()
        self.assertFalse(self.path.exists())
        self.assertFalse(self.receipt_path.exists())

    def test_conflicting_icon_sources_fail_before_any_download(self):
        path = self.manifest([{"iconKey": self.key},
                              {"iconKey": self.key, "artworkSource": self.item_source}])
        with patch.object(sys, "argv", ["cache-icons", "--manifest", str(path), "--dest", str(self.folder)]), \
                patch.object(icons, "download") as download, \
                self.assertRaisesRegex(ValueError, "Conflicting"):
            icons.main()
        download.assert_not_called()

    def test_duplicate_source_properties_are_rejected(self):
        path = self.folder / "manifest.json"
        source = json.dumps(self.item_source)
        path.write_text('{"schema":"warcraftsim-forever-talent-reference-v1","records":['
                        '{"iconKey":"inv_sword_07","artworkSource":' + source
                        + ',"artworkSource":' + source + '}]}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            icons.icon_sources(path)
        self.request.assert_not_called()


if __name__ == "__main__":
    unittest.main()
