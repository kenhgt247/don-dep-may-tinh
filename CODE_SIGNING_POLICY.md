# Code Signing Policy / Chính sách ký số

**Project:** Dọn Dẹp Máy Tính (Vietnamese for "Computer Cleanup") — a free, open-source Windows disk-cleanup tool.
**Repository:** https://github.com/kenhgt247/don-dep-may-tinh
**License:** MIT

## Status

Releases are currently **unsigned**. The project applied to the [SignPath Foundation](https://signpath.org) open-source program in October 2026; the application was declined for now because the project was new and had no public adoption yet. The project will re-apply once it has a wider user base.

When signing is approved, Windows release artifacts will carry the following attribution:

> Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org)

## What is signed

- `Don Dep May Tinh.exe` — the application executable (C#, .NET Framework 4.8) inside `DonDepMayTinh.zip`.

Only artifacts built from this repository by the GitHub Actions workflow
[`.github/workflows/release.yml`](.github/workflows/release.yml) are submitted for signing.
Nothing built on a personal computer is ever signed. Third-party tools (for example rclone,
which the app downloads from rclone.org only when the user chooses Google Drive) are not
part of the release and are not signed by this project.

## Roles

This is currently a single-maintainer project.

| Role | Members |
|---|---|
| Committers and reviewers | [kenhgt247](https://github.com/kenhgt247) |
| Approvers | [kenhgt247](https://github.com/kenhgt247) |

- Outside contributions are accepted only through pull requests reviewed by the maintainer before merge.
- Every signing request is manually approved by the maintainer.
- The maintainer's GitHub account uses two-factor authentication.

## Key protection

The project never receives or stores the private signing key. Signing is performed by
SignPath.io under the approved project and artifact configuration.

If a signed artifact, the release workflow, the maintainer account or a signing request is
suspected to be compromised, the project will stop releasing, investigate, notify the
SignPath Foundation and request revocation where appropriate.

## Privacy

See the [Privacy Policy](PRIVACY.md).

> This program will not transfer any information to other networked systems unless
> specifically requested by the user or the person installing or operating it.
