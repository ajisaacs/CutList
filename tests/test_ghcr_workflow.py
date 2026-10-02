"""Focused publishing-contract checks; real builds are verified separately."""

from __future__ import annotations

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = REPO_ROOT / ".github/workflows/build-cutlist.yml"


class GhcrWorkflowTests(unittest.TestCase):
    def test_publishes_sha_and_latest_from_the_existing_root_context(self) -> None:
        source = WORKFLOW.read_text()
        self.assertIn("IMAGE: ghcr.io/ajisaacs/cutlist", source)
        self.assertIn('docker build --pull -f CutList.Web/Dockerfile', source)
        self.assertIn('--tag "$IMAGE:$GITHUB_SHA" --tag "$IMAGE:latest" .', source)
        self.assertIn('docker push "$IMAGE:$GITHUB_SHA"', source)
        self.assertIn('docker push "$IMAGE:latest"', source)
        self.assertIn('org.opencontainers.image.source=https://github.com/ajisaacs/CutList', source)

    def test_non_master_and_fork_runs_cannot_publish(self) -> None:
        source = WORKFLOW.read_text()
        self.assertIn("branches: [master]", source)
        self.assertIn("github.repository == 'ajisaacs/CutList' && github.ref == 'refs/heads/master'", source)
        self.assertNotIn("pull_request", source)
        self.assertIn("cancel-in-progress: false", source)

    def test_uses_scoped_ephemeral_auth_and_pinned_checkout(self) -> None:
        source = WORKFLOW.read_text()
        self.assertIn("contents: read\n  packages: write", source)
        self.assertRegex(source, r"uses: actions/checkout@[0-9a-f]{40}\b")
        self.assertIn("persist-credentials: false", source)
        self.assertIn("GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}", source)
        self.assertNotIn("REGISTRY_TOKEN", source)
        self.assertNotIn("write-all", source)

    def test_checks_private_visibility_before_promoting_latest_and_reads_back_tags(self) -> None:
        source = WORKFLOW.read_text()
        self.assertIn('test "$visibility" = private', source)
        self.assertLess(source.index('test "$visibility" = private'), source.index('docker push "$IMAGE:latest"'))
        self.assertIn('docker manifest inspect "$IMAGE:$GITHUB_SHA"', source)
        self.assertIn('docker manifest inspect "$IMAGE:latest"', source)
        self.assertIn('cmp "$RUNNER_TEMP/cutlist-sha.json" "$RUNNER_TEMP/cutlist-latest.json"', source)

    def test_local_credentials_and_build_outputs_are_not_in_docker_context(self) -> None:
        patterns = set((REPO_ROOT / ".dockerignore").read_text().splitlines())
        self.assertTrue({".git", ".hermes", ".env", ".env.*", "**/bin", "**/obj"} <= patterns)


if __name__ == "__main__":
    unittest.main()
