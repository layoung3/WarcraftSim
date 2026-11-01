import importlib.util
import json
import pathlib
import tempfile
import unittest


SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "cache_forever_talent_icons.py"
SPEC = importlib.util.spec_from_file_location("cp174_icon_cache", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Cp174PinnedIconCacheTests(unittest.TestCase):
    def test_only_manifest_icon_keys_are_selected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / "manifest.json"
            path.write_text(json.dumps({
                "schema": "warcraftsim-forever-talent-reference-v1",
                "records": [
                    {"iconKey": "spell_fire_flamebolt"},
                    {"iconKey": "spell_fire_flamebolt"},
                    {"iconKey": "SPELL_NOT_ALLOWED"},
                    {"iconKey": "../traversal"},
                    {"iconKey": "inv_misc_gem_01"},
                ],
            }), encoding="utf-8")
            self.assertEqual(
                ["inv_misc_gem_01", "spell_fire_flamebolt"],
                MODULE.icon_keys(path),
            )

    def test_unknown_manifest_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / "manifest.json"
            path.write_text(json.dumps({"schema": "unknown", "records": []}),
                            encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Unsupported"):
                MODULE.icon_keys(path)


if __name__ == "__main__":
    unittest.main()
