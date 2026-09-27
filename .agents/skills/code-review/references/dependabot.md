# Dependabot fast path

Dependency bumps are most of this repo's PRs. The question is only "does the app still build, test, and behave the same?", so replace the nine axes with this checklist, then run Step 4 for the touched bucket.

## 1. Identify the ecosystem from the touched files

| Ecosystem | Manifest → lockfile that must move together | Bucket |
|---|---|---|
| nuget | `Directory.Packages.props` (CPM; a `.csproj` must never gain a `Version=`) | dotnet |
| github-actions | `.github/workflows/*.yml` (SHA + version comment, no lockfile) | ci |
| npm | `src/Garage.Web/package.json` → `package-lock.json` | web |
| docker | `src/Garage.Web/Dockerfile`, `src/Garage.FeatureFlags/flags-api.Dockerfile` | web / go |
| gomod | `src/Garage.FeatureFlags/go.mod` → `go.sum` | go |
| uv | `src/Garage.ChatService/pyproject.toml` → `uv.lock` | python |

Manifest changed without its lockfile → **Blocker**: CI's `npm ci`, `uv sync --frozen`, or `go test` fails on it.

## 2. Ecosystem-specific checks

**github-actions**: each changed `uses:` keeps a 40-char SHA and the trailing `# vX.Y.Z` comment moved to the same release. Verify one at random: `gh api repos/<owner>/<repo>/git/ref/tags/<tag> --jq .object` — an annotated tag (`type: tag`) needs `gh api repos/<owner>/<repo>/git/tags/<sha> --jq .object.sha` to reach the commit. SHA and comment disagreeing → **Blocker** (the pin is the security control). Check the action's `permissions` and required inputs did not change across a major.

**nuget**: the `aspire` group bumps `Aspire.*` and `CommunityToolkit.Aspire.*` in `Directory.Packages.props` but Dependabot never edits the `<Project Sdk="Aspire.AppHost.Sdk/<version>">` attribute in `src/Garage.AppHost/Garage.AppHost.csproj`; Sdk and `Aspire.Hosting.*` drifting apart across a minor → **Should**, name the line. `CommunityToolkit.Aspire.*` tracks its own version line; check its release notes state compatibility with the `Aspire.*` version in the file.

**npm**: `react`, `react-dom`, `@types/react`, `@types/react-dom` move as one (the `react` group); `@opentelemetry/*` packages share two version lines (stable `2.x`, experimental `0.x`) and an SDK bump without its matching exporter/instrumentation bump breaks the browser pipeline at runtime, not at build → **Should**, ask for a manual run.

**docker**: a digest-only bump keeps the tag; a tag change on `node:` must match `node-version` in `ci.yml`, and on `golang:` must match the `go` directive in `go.mod`. Mismatch → **Blocker**.

**gomod**: the `go` directive did not silently rise past the toolchain in `flags-api.Dockerfile` and `actions/setup-go`'s `go-version-file`.

**uv**: `requires-python` unchanged; `opentelemetry-*` stable (`1.x`) and instrumentation (`0.xxb0`) lines bumped in step as in npm.

## 3. Breaking-change scan (any major bump)

Read the release notes in the PR body, or `gh release view <tag> --repo <owner>/<repo>`. For each breaking change, grep this repo for the API it names. A hit the bump does not also fix → **Blocker**, cite the release-note line and our call site. No hits → say so explicitly; do not write "looks fine".

## 4. Group hygiene

A package outside every `groups.*.patterns` entry in `.github/dependabot.yml` arrives as a solo PR. When it clearly belongs with an existing group → **Nit** suggesting the pattern.

## 5. Verdict

Checks green and the scan found no hits → APPROVE. Major bump whose behaviour only shows at runtime (OTel pipelines, Aspire hosting, provider SDKs) → COMMENT, listing the manual run to do. Any Blocker → REQUEST CHANGES.
