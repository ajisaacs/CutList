# Repository workflow

## Primary repository

GitHub is the source of truth for CutList:

- Repository: https://github.com/ajisaacs/CutList
- Git remote: `https://github.com/ajisaacs/CutList.git`
- Default branch: `master`
- Push commits and tags, open and merge pull requests, track issues, and publish future releases on GitHub.

For an existing clone, run from its repository directory:

```bash
git remote set-url origin https://github.com/ajisaacs/CutList.git
git fetch origin
git remote -v
```

For a new clone:

```bash
git clone https://github.com/ajisaacs/CutList.git
```

## Gitea backup

https://git.thecozycat.net/aj/CutList is a read-only pull mirror of GitHub, configured to synchronize hourly. It backs up Git branches and tags, not GitHub issues, pull requests, comments, releases, release assets, or Actions configuration/secrets. Pushes to this mirror are not supported; all development belongs on GitHub.

The original Gitea repository is preserved read-only at https://git.thecozycat.net/aj/CutList-before-github-primary. Its history and repository metadata have not been deleted. On the 2026-10-02 cutover, all 252 existing commits on `master` were copied to GitHub without rewriting history; there were no tags, issues, pull requests, releases, or wiki pages to import. The original tip was `bf4297ab7ff47d51221b2df5f1188343918e5b2c`.

Do not re-enable a Gitea-to-GitHub push mirror: it could overwrite GitHub-side changes or remove GitHub-only branches. Restoring the old repository as writable would require an explicit, coordinated reversal of this workflow, not merely unarchiving it.

## Container publishing: GHCR

The active workflow is `.github/workflows/build-cutlist.yml`. It builds `CutList.Web/Dockerfile` with the repository root as the build context and publishes to GitHub Container Registry:

- `ghcr.io/ajisaacs/cutlist:latest`
- `ghcr.io/ajisaacs/cutlist:<full-commit-SHA>`

Images are **public**, explicitly requested by the owner so anyone can download them without credentials. The workflow uses the built-in `GITHUB_TOKEN` with only `contents: read` and `packages: write`; no custom registry secret is required. The source label links the package to this repository. Before uploading, the workflow verifies the existing package is public; it then publishes the commit tag and `latest`, reads back both manifests to confirm they match, and logs out before testing anonymous pulls.

Relevant pushes to `master` trigger publishing: web/core source, solution/build configuration, Docker context exclusions, the workflow, or its contract tests. Manual runs are also available:

```bash
gh workflow run build-cutlist.yml --repo ajisaacs/CutList --ref master
```

The publishing job refuses non-`master` refs and fork repositories. A serialized concurrency group prevents overlapping publishers. Checkout is pinned to a commit and does not persist credentials; `.dockerignore` excludes local credentials, Git/agent metadata, and build outputs.

### Pulling public images

No GitHub account, token, or registry login is required:

```bash
docker pull ghcr.io/ajisaacs/cutlist:latest
# For a repeatable rollout, choose the full commit-SHA tag instead.
```

Publishing does **not** change or restart the running CutList application, its database, or its deployment image reference. Existing deployments using `git.thecozycat.net/internal/cutlist` will remain on that registry until a separately verified rollout switches them. Keep the old registry images for rollback; do not delete them or change visibility/access to the old registry packages.

Package settings: https://github.com/users/ajisaacs/packages/container/cutlist/settings. If the GHCR package is removed and recreated, confirm its visibility is public in those settings before rerunning this workflow; the preflight deliberately fails closed when package metadata is unavailable or not public.

The legacy `.gitea/workflows/build-cutlist.yml` is retained as a historical reference. It does not run on the read-only mirror, and the original repository remains archived. The old Actions-secrets migration blocker no longer applies to publishing because GHCR uses the workflow token; pushing workflow changes still requires GitHub Workflows write permission.

Next hardening: consider digest-pinned deployment rollouts and image vulnerability scanning after the publishing baseline is verified. The baseline image build reports NU1903 for the existing `Microsoft.OpenApi` 2.4.1 dependency (high severity, `GHSA-v5pm-xwqc-g5wc`); dependency remediation is separate from this registry migration.
