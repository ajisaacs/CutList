"""Regression checks for compact CutList identifiers."""

from __future__ import annotations

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
JOB_SERVICE = REPO_ROOT / "CutList.Web/Services/JobService.cs"
UNPAD_MIGRATION = REPO_ROOT / "CutList.Web/Migrations/20260802212500_RemoveJobNumberPadding.cs"


class JobNumberFormatTests(unittest.TestCase):
    def test_new_jobs_use_the_database_id_without_zero_padding(self) -> None:
        service = JOB_SERVICE.read_text()

        self.assertIn('job.JobNumber = $"JOB-{job.Id}";', service)
        self.assertNotIn("D5", service)
        self.assertNotIn("MaxAsync() as string", service)

    def test_existing_job_numbers_are_converted_to_their_database_ids(self) -> None:
        migration = UNPAD_MIGRATION.read_text()

        self.assertIn("SET JobNumber = 'JOB-' + CAST(Id AS varchar(11))", migration)


if __name__ == "__main__":
    unittest.main()
