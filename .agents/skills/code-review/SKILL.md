---
name: code-review
description: "Review a pull request or branch of this repo before merge: pins the diff (PR number via gh, else branch vs merge-base with main), routes Dependabot bumps to a dedicated checklist, runs the same per-language checks CI runs, and reports severity-ranked findings with a merge verdict. USE FOR: \"review this PR\", \"review #123\", \"review my branch\", \"is this safe to merge\", \"check this Dependabot PR\", pre-merge review of .NET, Go, Python, React, flagd.json, or GitHub Actions changes in this repo. DO NOT USE FOR: implementing or fixing the findings (edit directly, or address-pr-comments), opening the PR (create-pull-request), deep MSBuild project-file audits (msbuild-code-review), test-suite quality audits (test-anti-patterns, grade-tests), or writing new .NET code to a style guide (dotnet-best-practices)."
---

# Code Review

Pre-merge review of a PR or branch in this polyglot Aspire repo. Read-only: it runs checks and reports. It edits no code, never runs `aspire run`, and posts nothing to GitHub unless the request says to.

| Input | Required | Description |
|---|---|---|
| PR number / URL | No | Reviewed via `gh`; when absent, the current branch is reviewed against `main` |
| "post to the PR" | No | Only an explicit instruction in the request enables Step 5's `gh pr review` |

## Step 1: Pin the diff

| Input | Commands |
|---|---|
| PR number or URL | `gh pr view <n> --json number,title,body,author,baseRefName,headRefName,files,commits`, then `gh pr diff <n>`. Without `gh`, use the GitHub MCP `pull_request_read` (`get`, `get_diff`, `get_files`). |
| Nothing given | `git rev-parse --verify main`, `git log main..HEAD --oneline`, `git diff main...HEAD` (three-dot: merge-base). `git fetch origin main` first when `origin/main` is ahead. |

Stop and report when the ref does not resolve or the diff is empty. An empty diff is a result, not a review.

Classify touched paths into buckets using the `changes` filters from `.github/workflows/ci.yml`:

| Bucket | Paths |
|---|---|
| dotnet | `src/Garage.ApiService/**`, `src/Garage.ApiDatabaseSeeder/**`, `src/Garage.ApiModel/**`, `src/Garage.AppHost/**`, `src/Garage.ServiceDefaults/**`, `src/Garage.Shared/**`, `**/*.props`, `*.slnx`, `global.json` |
| go | `src/Garage.FeatureFlags/**` |
| python | `src/Garage.ChatService/**` |
| web | `src/Garage.Web/**` |
| ci | `.github/**` |
| flags | `src/Garage.AppHost/flags/flagd.json`, `src/Garage.ChatService/prompts/**`, any hunk adding or changing a flag evaluation call (`GetBooleanValueAsync`, `GetIntegerValueAsync`, `GetStringValueAsync`, `useBooleanFlagValue`, `useStringFlagValue`, `useNumberFlagValue`, `get_boolean_value`, `get_string_value`, `get_integer_value`, `BooleanValue(`, `StringValue(`, `IntValue(`) |

A path under `src/` that lands in no bucket is a finding in its own right (CI-gate integrity, below): CI never builds it.

## Step 2: Route

Dependabot PR (author `dependabot[bot]`, branch `dependabot/**`, or title starting `build(deps` / `Bump `): read `references/dependabot.md`, run its checklist plus Step 4 for the touched bucket, then report. Skip Step 3.

Anything else: Step 3.

## Step 3: Review

Apply the universal axes below to every hunk, then load one reference per touched bucket: `references/dotnet.md`, `references/go.md`, `references/python.md`, `references/web.md`, `references/flags.md` (the ci bucket is covered inline). When sub-agent dispatch is available and more than one bucket is touched, run one sub-agent per bucket with its reference file and only the hunks under its paths, then aggregate without re-ranking across buckets.

Formatting and lint belong to Step 4 (`dotnet format`, `gofmt`, `eslint`, `tsc`, `black`); report them there once, never as findings here.

**Correctness**: logic errors, unhandled nulls, swallowed exceptions or errors, async misuse (missing `await`, sync-over-async, blocking IO on an async path), `CancellationToken` / `ctx` not propagated, limits and counts off by one.

**Security**: OWASP Top 10 on every new input; secrets in code, config, logs, or `.prompt.yml`; user input reaching a system prompt; a GitHub Actions `uses:` without a 40-char SHA and trailing `# vX.Y.Z` comment; job `permissions` beyond `contents: read` without a stated need.

**Repo standards** (`.github/copilot-instructions.md`): conventional-commit PR title, or every commit subject when reviewing a branch (`feat:`, `fix:`, `build(deps):`, …), and a subject that names each behaviour change in the commit; kebab-case flag keys; DTOs in `Garage.Shared`; cross-service wiring in `Garage.ServiceDefaults`; every new AppHost env var documented beside the service that reads it.

**CI-gate integrity** (`.github/workflows/ci.yml`): a new job appears in `ci-gate.needs`, otherwise it is advisory and a red job can merge; a new `src/Garage.*` directory appears in the `changes` filters, otherwise CI silently never builds it; no workflow-level `paths:` filter (a skipped workflow leaves the required `CI Gate` check pending forever); `ALLOWED_SKIPS` only gains jobs that are skipped by design.

**Tests**: Go and Python changes ship with tests (`*_test.go`, `test_*.py`) that exercise the new branch. .NET and web have no test projects today: report a missing test there as Should, never Blocker.

**Docs**: a new flag, prompt, command, service, or env var lands in `.github/copilot-instructions.md` (flags list, project structure, common tasks) and in `README.md` wherever it already describes that area.

**Spec** (only when the PR body or a commit references an issue: `#123`, `Closes #45`): fetch it with `gh issue view <n>` or the GitHub MCP `issue_read`; report requirements missing or partial, behaviour nobody asked for, and requirements implemented wrongly, quoting the issue line. With no reference, write `Spec: no linked issue` and move on.

## Step 4: Run what CI runs

Only for touched buckets, on a checkout of the PR head. `gh pr checkout <n>` only when `git status --porcelain` is empty; otherwise every check is *not run: dirty working tree*.

| Bucket | Commands (repo root unless noted) |
|---|---|
| dotnet | `dotnet restore` · `dotnet format --verify-no-changes --no-restore` · `dotnet build --configuration Release --no-restore` |
| go | in `src/Garage.FeatureFlags`: `gofmt -l .` (any output is a failure) · `go test ./...` |
| python | in `src/Garage.ChatService`: `uv sync --frozen --group dev` · `uv run pytest` · `uvx black --check .` (documented standard CI does not run) |
| web | in `src/Garage.Web`: `npm ci` · `npm run lint` · `npm run build` |

Record each command as **pass**, **fail** (quote the first error), or **not run** (tool missing, dirty tree, network). A check that did not run is reported as not run, never as pass.

Attribute every failure before it reaches the verdict: re-run the failing tool on the `main` version of the same files (`git show main:<path> | <tool> --check -` or an equivalent that leaves the working tree alone). A failure that also fails on `main` is **pre-existing**: report it once as Should tagged *pre-existing*, propose the separate fix (a format-only PR, a missing CI step), and leave it out of the verdict.

## Step 5: Report

Scale the report to the diff: a two-line bump earns a paragraph, not a dashboard. Post to GitHub only when the request explicitly asks, with `gh pr review <n> --comment --body-file <report>`; use `--request-changes` or `--approve` only when the user names that action.

```
## Review: <PR title or branch> (<n> commits, <m> files; buckets: <list>)
Route: standard | dependabot

### Checks
| Bucket | Command | Result |

### Findings
| # | Severity | Location | Axis | Finding | Fix |
(sorted Blocker → Should → Nit; the single line "No findings." when empty)

Spec: <summary> | no linked issue
Verdict: REQUEST CHANGES | COMMENT | APPROVE — <one-line reason>
```

| Severity | Meaning | Examples |
|---|---|---|
| Blocker | Fix before merge | bug, security issue, failing or unrunnable-by-design check, new compiler warning (`TreatWarningsAsErrors`), flag evaluated but absent from `flagd.json`, CI-gate wiring missing, lockfile out of step with its manifest |
| Should | Fix now or in a named follow-up | missing test for a Go/Python change, missing telemetry on a new operation, docs drift, unsafe code-side flag default |
| Nit | Optional | naming, comment wording, style that no tool enforces |

Verdict rule: any Blocker → REQUEST CHANGES; else any Should → COMMENT; else APPROVE.

## Validation

- [ ] Diff pinned to a resolved ref and non-empty, or the skill stopped and said so
- [ ] Every touched bucket has a checks row; nothing reads pass without having run
- [ ] Every failing check is attributed: pre-existing failures are tagged and left out of the verdict
- [ ] Every finding names file:line, severity, axis, and a fix
- [ ] Verdict follows the severity rule
- [ ] Nothing posted to GitHub without an explicit request

## Common Pitfalls

| Pitfall | Do instead |
|---|---|
| Two-dot `git diff main..HEAD` counts commits on `main` as part of the branch | Three-dot `main...HEAD` |
| `gh pr checkout` on a dirty tree mixes local edits into the review | Check `git status --porcelain` first; report checks as not run |
| Reading a lockfile hunk line by line | Check manifest ↔ lockfile move together, then run the install in Step 4 |
| A 1,000-line diff reviewed in one pass | One sub-agent per bucket, each with its reference file |
