# Release Guide

## Version Numbering

All four packages share a unified version number and are always released together.

| Change type | Bump | Example |
|-------------|------|---------|
| Bug fixes, test/CI improvements, documentation updates, performance improvements (no API changes) | Patch | 1.0.0 → 1.0.1 |
| New public API, new features, new packages (backward-compatible) | Minor | 1.0.1 → 1.1.0 |
| Breaking API changes (removed or modified public types/members) | Major | 1.1.0 → 2.0.0 |

## Release Process

1. Update `<Version>` in all four `.csproj` files to the new version.
2. Commit: `chore: bump all packages to v{version}`
3. Push to `main`.
4. Create a GitHub Release with tag `v{version}` — this triggers the CD workflow, which builds, tests, and publishes all four packages to NuGet.org.

## Release Title

```
v{version}
```

Example: `v1.0.1`

## Release Body Template

```markdown
## What's Changed

### Bug Fixes
- ...

### New Features
- ...

### Breaking Changes
- ...

---

## Packages

All packages updated to `v{version}`:
- `Lunarium.Logging`
- `Lunarium.Logging.Hosting`
- `Lunarium.Logging.Configuration`
- `Lunarium.Logging.Http`

---

## Migration Guide

...
```

**Omit any section that has no content.** "New Features", "Breaking Changes", and "Migration Guide" should only appear when there is something to say. A missing section means nothing changed in that category.

## Notes for Claude

When asked to write a release:
1. Run `git log v{previous_tag}..HEAD --oneline` to get the list of commits.
2. Classify each commit into Bug Fixes / New Features / Breaking Changes.
3. Fill in the template above, omitting empty sections.
4. Language: English only.
