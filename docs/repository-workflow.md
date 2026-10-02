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

## Container publishing: pending migration

The legacy workflow at `.gitea/workflows/build-cutlist.yml` built and pushed `git.thecozycat.net/internal/cutlist:latest` and a commit-SHA tag using the Gitea Actions `REGISTRY_TOKEN` secret. That workflow is retained as a reference, but Actions is disabled on the read-only backup mirror and the original repository is archived.

The repository move does not restart or change the running CutList application, its database, or existing container images. Future GitHub pushes do not automatically publish an image yet.

The GitHub credential available during the cutover can push repository content but returns HTTP 403 for the Actions secrets API. To finish migrating container publishing:

1. Give the credential used for `ajisaacs/CutList` repository Secrets read/write permission and Workflows write permission, or configure the workflow/secrets through the GitHub UI.
2. Add a suitable registry-publishing credential as GitHub Actions secret `REGISTRY_TOKEN`; keep the value out of Git history, documentation, and logs. Use an explicitly configured registry username rather than assuming the GitHub actor is a Gitea user.
3. Adapt the legacy workflow into `.github/workflows/`, replacing Gitea contexts with GitHub contexts while retaining the private internal registry/image destination.
4. Exercise a build/push, verify both the SHA and `latest` image tags in the registry, and separately verify any intended application update before claiming publishing/deployment is restored.

Do not switch to a public image namespace, broaden registry access, or restart the deployed application as part of the repository migration.
