# Code Signing Policy

## Status

As of 2026-11-10, an application for the SignPath Foundation free OSS code
signing subscription has **not yet been submitted**. No released binary claims a
SignPath signature. Released installers (`v1.1`, `v1.1.1`) are unsigned.

A self-signed development certificate exists on the maintainer's local machine
only. It is used for local Authenticode verification tests, is not trusted
outside that machine, and its signatures must never be treated as production
signatures or shipped as a release claim.

If the application is accepted, the attribution will read:
*Free code signing provided by SignPath.io, certificate by SignPath Foundation.*
The publisher shown by Windows will be **SignPath Foundation**, not the
individual maintainer.

## Roles and responsibilities

- Author, reviewer, and release approver: **naixi000777** (single maintainer).
- All external contributions must be reviewed by the maintainer before they
  enter the release branch.
- Multi-factor authentication is required on both the GitHub account and the
  SignPath account before production signing is enabled. This is a requirement,
  not a claim that the configuration has already been verified.
- Every production signing request requires explicit manual approval in SignPath.

## Trusted build and provenance

- All jobs leading up to a signing request run on **GitHub-hosted Windows
  runners** (`windows-latest`). Self-hosted runners are not used.
- Builds are produced from the public repository
  `https://github.com/naixi000777/mouse-launcher` on branch `master`, from a
  specific commit recorded by the workflow.
- The build is performed by `scripts/build-ci.ps1` inside the GitHub Actions
  workflow. Binaries compiled locally and uploaded afterwards do **not**
  establish build origin and are never submitted for signing.
- Unsigned artifacts are stored as GitHub workflow artifacts before a signing
  request is submitted.
- SignPath API tokens are kept in GitHub Actions secrets with submitter-only
  permissions. Tokens are never committed to the repository and private signing
  keys are never requested or handled by maintainers.

## Signing scope

Only the following project-owned binaries are signed:

- `老鼠启动器.exe` (main GUI application)
- `mlc.exe` (CLI build of the same source)
- `老鼠启动器-安装包.exe` (self-extracting installer)

`Newtonsoft.Json.dll` is a third-party MIT-licensed library (see
`THIRD_PARTY_NOTICES.md`). It is **not** signed under this project's
subscription and its upstream license and notices are preserved. The file was
verified byte-identical to `lib/net45/Newtonsoft.Json.dll` from the official
NuGet package `Newtonsoft.Json 13.0.3` (assembly version 13.0.0.0, 711952
bytes, SHA-256
`E1E27AF7B07EEEDF5CE71A9255F0422816A6FC5849A483C6714E1B472044FA9D`), so its
provenance is independently verifiable. This is disclosed to SignPath as part
of the eligibility review.

## Signing order

The installer embeds `payload.zip` as a .NET manifest resource at compile time.
Signing only the outer installer would leave the extracted application unsigned.
Releases therefore follow this order:

1. Build the GUI and CLI executables.
2. Submit them for signing (request #1).
3. Replace the unsigned copies with the signed ones.
4. Rebuild `payload.zip` from the signed executables.
5. Recompile the installer so the new `payload.zip` is embedded (request #2 input).
6. Submit the installer for signing (request #2).
7. Verify every binary before publication.

Requests #1 and #2 each require manual approval. A dry-run using a separate
test-signing policy is acceptable, but test-signed output must never enter a
release candidate or a GitHub Release.

## Verification before publication

- `Get-AuthenticodeSignature` status must be `Valid` for all three binaries.
- An RFC3161 trusted timestamp must be present.
- SHA-256 of the published asset is recorded and compared with the workflow
  artifact.
- Product name and version metadata must match the release tag.
- An existing release asset is not replaced without separate release
  authorization.

## What signing does not do

An OV-level certificate does not grant instant Microsoft Defender SmartScreen
reputation. Early downloads may still show an "unrecognized app" warning until
reputation accumulates across clean installs. Signing identifies the publisher
and detects tampering; it does not replace code review, dependency maintenance,
or testing.
