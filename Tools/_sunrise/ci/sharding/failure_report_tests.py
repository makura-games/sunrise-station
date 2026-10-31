#!/usr/bin/env python3

import io
import importlib.util
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("failure_report.py")
SPEC = importlib.util.spec_from_file_location("failure_report", SCRIPT_PATH)
FAILURE_REPORT = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(FAILURE_REPORT)


class FailureReportTests(unittest.TestCase):
    def test_preserves_primary_assertion_and_dirty_dispose(self):
        lines = [
            "Failed Content.IntegrationTests.Tests.Round.JobTest.JobWeightTest [585 ms]",
            "  Error Message:",
            "   Multiple failures or warnings in test:",
            "   1) Assert.That(engineerWeight, Is.EqualTo(passengerWeight))",
            "   Expected: 0",
            "   But was: 10",
            "   2) Test was dirty-disposed.",
            "  Stack Trace:",
            "   at Content.IntegrationTests.Tests.Round.JobTest.JobWeightTest()",
            "Passed AnotherTest [1 ms]",
        ]

        blocks = FAILURE_REPORT.extract_failure_blocks(lines)

        self.assertEqual(len(blocks), 1)
        block = "\n".join(blocks[0])
        self.assertIn("Expected: 0", block)
        self.assertIn("But was: 10", block)
        self.assertIn("Test was dirty-disposed.", block)

    def test_omits_routine_standard_output(self):
        lines = [
            "Failed Example [1 ms]",
            "  Error Message:",
            "   Expected: 1",
            "   But was: 2",
            "  Standard Output Messages:",
            "SERVER: routine warning",
            "CLIENT: routine warning",
            "Failed! - Failed: 1, Passed: 0",
        ]

        block = "\n".join(FAILURE_REPORT.extract_failure_blocks(lines)[0])

        self.assertIn("Expected: 1", block)
        self.assertNotIn("routine warning", block)

    def test_report_does_not_change_when_log_has_no_failures(self):
        path = Path(__file__).with_name("failure-report-test.log")
        path.write_text("Passed Example [1 ms]\n", encoding="utf-8")
        try:
            output = io.StringIO()
            self.assertFalse(FAILURE_REPORT.report_failures(path, output))
            self.assertEqual(output.getvalue(), "")
        finally:
            path.unlink()

    def test_handles_ansi_colours_in_result_header(self):
        lines = [
            "\x1b[31mFailed Example [1 ms]\x1b[0m",
            "  Error Message:",
            "   Expected: 1",
            "   But was: 2",
        ]

        blocks = FAILURE_REPORT.extract_failure_blocks(lines)

        self.assertEqual(len(blocks), 1)
        self.assertIn("Expected: 1", "\n".join(blocks[0]))

    def test_ignores_routine_server_exception_without_failed_test(self):
        lines = [
            "SERVER: [WARN] cfg: Exception: optional provider is unavailable",
            "Passed Example [1 ms]",
        ]

        self.assertEqual(FAILURE_REPORT.extract_failure_blocks(lines), [])

    def test_marks_teardown_only_diagnostics_as_incomplete(self):
        path = Path(__file__).with_name("failure-report-test.log")
        path.write_text(
            "Failed Example [1 ms]\n"
            "  Error Message:\n"
            "   Test was dirty-disposed.\n"
            "  Stack Trace:\n"
            "   at OnDirtyDispose()\n",
            encoding="utf-8",
        )
        try:
            output = io.StringIO()
            self.assertTrue(FAILURE_REPORT.report_failures(path, output))
            self.assertIn("No primary assertion marker", output.getvalue())
        finally:
            path.unlink()


if __name__ == "__main__":
    unittest.main()
