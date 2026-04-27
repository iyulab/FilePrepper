# Security Policy

## Supported Versions

FilePrepper follows a rolling support model — the latest released minor version receives security fixes. Older minors are supported on a best-effort basis only.

| Version | Supported          |
| ------- | ------------------ |
| 0.6.x   | :white_check_mark: |
| < 0.6   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in FilePrepper — including in a transitive dependency surfaced as a `NU1903` warning to consumers — please report it through one of the following channels, in order of preference:

1. **GitHub Security Advisories** (preferred): Open a private advisory at <https://github.com/iyulab/FilePrepper/security/advisories/new>. This keeps the report private until a fix is published.
2. **Public issue**: If the issue does not provide a meaningful exploitation primitive on its own (for example, a transitive CVE that is already public via a GHSA advisory), filing a regular GitHub issue at <https://github.com/iyulab/FilePrepper/issues> is acceptable and often faster.

Please include:

- The affected FilePrepper version (`Directory.Build.props` `<Version>`).
- The dependency chain reproducing the issue, ideally captured via `dotnet nuget why <consumer.csproj> <vulnerable-package>`.
- Links to upstream advisories (CVE / GHSA) where applicable.
- A minimal reproduction project if the issue is in FilePrepper code itself.

## Response expectations

- **Acknowledgement**: within 7 days of report.
- **Triage decision**: within 14 days. The maintainers will respond with one of: accepted (working on a patch), redirected (e.g., to upstream EPPlus), or declined (with reasoning).
- **Patch release**: for High/Critical-severity transitive CVEs, the goal is a patch release within 30 days of triage acceptance, dependent on upstream availability.

## Scope

In scope:

- The FilePrepper SDK (`FilePrepper` NuGet package) and CLI (`fileprepper-cli` dotnet tool).
- Transitive dependencies that surface security warnings to consumer projects (NU1903 / NU1901).

Out of scope:

- Vulnerabilities in consumer applications using FilePrepper.
- Issues that require physical or local-machine access to the developer environment.
- Generic advice on data preprocessing security (input validation, etc.).

## Defense in depth

The repository runs:

- `dotnet list package --vulnerable --include-transitive` as part of the publish workflow.
- Dependabot version updates and security alerts on the NuGet and GitHub Actions ecosystems.

These are intended as early-warning signals, not as a guarantee. Consumers should additionally pin or audit their own transitive trees for their threat model.
