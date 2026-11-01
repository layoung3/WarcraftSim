"""CI regression: reviewed equipment source manifests must not collide silently."""
import unittest
import reviewed_intake_audit


class ReviewedIntakeAuditTests(unittest.TestCase):
    def test_registered_catalogs_have_only_declared_overrides(self):
        self.assertEqual(0, reviewed_intake_audit.audit())


if __name__ == "__main__":
    unittest.main()
