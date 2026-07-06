# Repository rulesets

These JSON files document the repository rulesets under
[Settings → Rules → Rulesets](https://github.com/P4suta/TateYoko/settings/rules).
GitHub does **not** apply them from the repo automatically — they are the source
of truth, applied via the API:

```sh
gh api --method POST repos/P4suta/TateYoko/rulesets \
  --input .github/rulesets/protect-default-branch.json
```

To update an existing ruleset, `PUT repos/P4suta/TateYoko/rulesets/<id>` with the
same file.

| Ruleset | Target | Effect |
| ------- | ------ | ------ |
| `protect-default-branch` | `main` | PR required (squash-only, 0 approvals), CI `test` must pass, linear history, no force-push, no deletion |
| `require-signed-commits` | `main` | commits on `main` must be signed (squash-merges are signed by GitHub) |
| `protect-release-tags` | `v*` tags | release tags cannot be force-moved or deleted (immutable releases) |

Everything reaches `main` through a squash-merged PR (GitHub authors that commit
and signs it) or a release-please API commit, so `require-signed-commits` is
satisfied without contributors having to sign locally.
