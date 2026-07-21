## Summary

<!-- What does this change, and why? -->

## Linear

Closes DEV-___
<!-- The Linear issue this PR resolves; requires the Linear GitHub integration. -->

## Checklist

- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `perf:`, `docs:`, …) — squash-merge uses it as the commit and in the release notes
- [ ] `mise exec -- dotnet test TateYoko.slnx` passes
- [ ] The hexagonal boundary is intact: `TateYoko.Core` still references neither PDFsharp nor WinUI
- [ ] If distribution/layout changed: `mise run publish` still produces a working bundle

See [CONTRIBUTING.md](../CONTRIBUTING.md).
