# Image Tag Convention Design

## Problem

Docker image tags don't track the underlying Docxodus library version, making it hard to know which library version a given image was built with.

## Tag Format

```
<service-version>-docxodus<lib-version>
```

Example: `1.2.0-docxodus5.4.2`

## Version Sources

- **Service version:** Extracted from the git tag that triggers the workflow. A `v1.2.0` tag produces service version `1.2.0`.
- **Docxodus version:** Extracted automatically from `<PackageReference Include="Docxodus" Version="..."/>` in `DocxodusService.csproj` at build time.

## Tags Produced

Each publish produces exactly two tags:

1. `<service-version>-docxodus<lib-version>` (e.g. `1.2.0-docxodus5.4.2`)
2. `latest`

## Workflow Changes

Changes are limited to `.github/workflows/publish.yml`:

1. Remove `on.push.branches` trigger — publish only on `v*` tags.
2. Add step to extract Docxodus version from csproj.
3. Add step to extract service version from git tag ref (`GITHUB_REF_NAME` minus the `v` prefix).
4. Replace `docker/metadata-action` with explicit tag construction.

## No Changes Required

- `ci.yml` — unchanged
- `Dockerfile` — unchanged
- Application code — unchanged
