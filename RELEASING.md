# Release Process

This document describes the automated release and versioning process for the Docker Compose Manager project.

## Overview

The project uses **semantic versioning** (SemVer) with automated version bumping based on PR labels. When a PR is merged to the `main` branch with a release label, a new version is automatically created and released.

## Versioning Strategy

- **Version Format**: `vMAJOR.MINOR.PATCH` (e.g., v1.2.3)
- **Current Version**: See the `VERSION` file in the repository root
- **Version Source of Truth**: Git tags
- **Semantic Versioning**: Following [SemVer 2.0.0](https://semver.org/)

### Version Bump Types

- **MAJOR** (v1.0.0 → v2.0.0): Breaking changes, incompatible API changes
- **MINOR** (v1.0.0 → v1.1.0): New features, backward-compatible changes
- **PATCH** (v1.0.0 → v1.0.1): Bug fixes, security patches, backward-compatible fixes

## Creating a Release

### Step 1: Create Your Pull Request

1. Create a feature branch from `main`
2. Make your changes
3. Push your branch and create a PR targeting `main`
4. Wait for CI checks to pass

### Step 2: Add Release Label

Add **one** of the following labels to your PR:

- `release-major` - For breaking changes (bumps MAJOR version)
- `release-minor` - For new features (bumps MINOR version)
- `release-patch` - For bug fixes (bumps PATCH version)

**Important**: Only one release label should be added per PR. If multiple labels are present, precedence is: major > minor > patch.

### Step 3: Merge the Pull Request

When you merge the PR to `main`, the release workflow automatically:

1. **Creates a git tag** with the new version (e.g., v0.2.0)
2. **Updates the VERSION file** in the repository
3. **Generates a CHANGELOG** with commit history
4. **Creates a GitHub Release** with release notes
5. **Triggers Docker image builds** with the new version tag

The entire process is fully automated - no manual intervention required!

## What Gets Released

When a new version is created:

### Backend (API)
- Version embedded in assembly metadata
- Accessible via `/api/system/version` endpoint
- Docker image tagged with version
- OCI labels with version, build date, and git commit

### Frontend (UI)
- Version injected at build time via Vite
- Displayed in the UI (via `VersionInfo` component)
- Docker image tagged with version
- OCI labels with version, build date, and git commit

### Docker Images
Both images are published to GitHub Container Registry (GHCR):
- `ghcr.io/{owner}/docker-compose-manager-backend:{version}`
- `ghcr.io/{owner}/docker-compose-manager-frontend:{version}`

Images are tagged with:
- Version tag (e.g., `0.2.0`)
- Major.Minor tag (e.g., `0.2`)
- Major tag (e.g., `0`)
- `latest` (for main branch releases)
- Git SHA (e.g., `sha-abc1234`)

## Release Workflow Details

The release process is handled by `.github/workflows/release.yml`:

```yaml
Trigger: PR merged to main with release-* label
↓
Determine version bump type from label
↓
Create new git tag (e.g., v0.2.0)
↓
Update VERSION file and commit to main
↓
Generate CHANGELOG from commits
↓
Create GitHub Release with notes
↓
Trigger docker-build-publish.yml workflow
↓
Build and publish Docker images with new version
```

## Version Information Access

### Backend (API)

The backend exposes version information via API:

```bash
curl http://localhost:5000/api/system/version
```

Response:
```json
{
  "success": true,
  "data": {
    "version": "0.2.0",
    "buildDate": "2025-01-15T10:30:00Z",
    "gitCommit": "abc1234",
    "environment": "Production"
  }
}
```

### Frontend (UI)

Import and use the `VersionInfo` component:

```tsx
import { VersionInfo } from '@/components/common'

// Simple badge
<VersionInfo />

// Detailed view
<VersionInfo showDetails />
```

Or access version info programmatically:

```typescript
import { APP_VERSION, BUILD_DATE, GIT_COMMIT } from '@/utils/version'

console.log(`App version: ${APP_VERSION}`)
```

### Docker Images

Inspect image labels:

```bash
docker inspect ghcr.io/{owner}/docker-compose-manager-backend:latest \
  --format '{{json .Config.Labels}}' | jq
```

Output includes:
- `org.opencontainers.image.version`
- `org.opencontainers.image.created`
- `org.opencontainers.image.revision`
- `org.opencontainers.image.source`

## Troubleshooting

### Release not created after PR merge

**Possible causes**:
1. PR missing release label - add `release-minor`, `release-major`, or `release-patch`
2. PR was closed without merging - re-open and merge
3. Workflow permissions issue - check Actions permissions in repo settings

### Version not updated in application

**Possible causes**:
1. Old Docker images cached - pull latest: `docker compose pull`
2. Environment variables not set - check GitHub Actions workflow logs
3. Build failed - check workflow status in Actions tab

### CHANGELOG not updated

The CHANGELOG is automatically generated from:
- Merged PR title and number
- Commit messages since last tag

Ensure commit messages are descriptive for better changelog quality.

## Manual Release (Emergency)

If the automated process fails, you can create a release manually:

```bash
# 1. Create and push tag
git tag v0.2.1
git push origin v0.2.1

# 2. Update VERSION file
echo "0.2.1" > VERSION
git add VERSION
git commit -m "chore: bump version to v0.2.1"
git push origin main

# 3. Create GitHub Release via web UI or gh CLI
gh release create v0.2.1 --title "Release v0.2.1" --generate-notes

# 4. Trigger Docker build manually
# Go to Actions tab → docker-build-publish.yml → Run workflow → Select tag v0.2.1
```

## Best Practices

### When to use each version bump

**PATCH (bug fix)**:
- Security patches
- Bug fixes that don't change functionality
- Performance improvements
- Documentation updates

**MINOR (feature)**:
- New features
- New API endpoints
- Enhanced functionality
- New UI components
- Backward-compatible changes

**MAJOR (breaking)**:
- Breaking API changes
- Removed endpoints or features
- Database schema changes requiring migration
- Major architectural changes
- Changes that require user action

### PR Guidelines for Releases

1. **Single concern**: One PR should address one feature/fix
2. **Descriptive titles**: PR titles become part of CHANGELOG
3. **Release notes**: Add detailed description in PR body
4. **Label correctly**: Choose appropriate version bump type
5. **Test thoroughly**: Ensure all tests pass before merge

### Semantic Commit Messages

While not required, using conventional commits helps generate better changelogs:

- `feat:` New feature (suggests MINOR bump)
- `fix:` Bug fix (suggests PATCH bump)
- `BREAKING CHANGE:` Breaking change (suggests MAJOR bump)
- `docs:` Documentation changes
- `chore:` Maintenance tasks

Example:
```
feat: add container log streaming via WebSocket

Implements real-time log streaming for running containers using
Socket.IO. Includes backend WebSocket handler and frontend component.

Closes #123
```

## Release Checklist

Before merging a PR with a release label:

- [ ] All CI checks pass (tests, linting, builds)
- [ ] Changes tested locally and in staging
- [ ] Breaking changes documented (if MAJOR bump)
- [ ] Migration guide provided (if needed)
- [ ] PR description explains changes clearly
- [ ] Correct release label applied
- [ ] Dependencies updated (if applicable)
- [ ] Documentation updated (if applicable)

## Version History

Version history is available in:
- **GitHub Releases**: Full release notes with changelog
- **CHANGELOG.md**: Cumulative changelog file
- **Git Tags**: All version tags

View releases: `https://github.com/{owner}/docker-compose-manager/releases`

## Questions or Issues

If you encounter any problems with the release process:
1. Check the [GitHub Actions logs](../../actions)
2. Review this document for troubleshooting steps
3. Open an issue with the `release` label
4. Contact maintainers for assistance

---

**Current Version**: See `VERSION` file or latest [GitHub Release](../../releases/latest)
