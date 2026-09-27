# Feature-flag hygiene

Flags are the product here. Every check below is a cross-file consistency check the diff alone cannot show, so grep the whole tree, not just the hunks.

Source of truth: `src/Garage.AppHost/flags/flagd.json` (flagd schema: `flags.<key>.{state,variants,defaultVariant,targeting}`). Evaluation sites: .NET `featureClient.Get{Boolean,Integer,String}ValueAsync`, Go `featureClient.{Boolean,String,Int}Value`, Python `client.get_{boolean,string,integer}_value`, React `use{Boolean,String,Number}FlagValue`.

| Check | Severity when it fails |
|---|---|
| Every key evaluated in code exists in `flagd.json` under exactly that spelling. An absent key evaluates to the code default with an error reason and nothing surfaces it. | Blocker |
| Every key added to `flagd.json` is evaluated somewhere, or the PR says it is demo-only. | Should |
| Key is kebab-case. | Should |
| Variant value type matches the SDK method: `true/false` ↔ Boolean; quoted string ↔ String; integer ↔ Integer/Number. A type mismatch returns the default with `TYPE_MISMATCH`. | Blocker |
| The code-side default is the safe behaviour when flagd is unreachable — normally the "off" path. A default of `true` for an "enable-*" key (e.g. `useBooleanFlagValue("enable-chatbot", true)` against `defaultVariant: "off"`) must be deliberate; ask for intent. | Should |
| `enable-preview-mode` is a comma-separated list of flag keys the UI toggles at runtime; each listed key exists, and a new runtime-toggleable flag is added to the `enabled` variant. | Blocker for an unknown key; Should for an omission |
| `prompt-file` variants ↔ `src/Garage.ChatService/prompts/<variant>.prompt.yml` exist one-to-one, and `prompt_loader.py` still resolves the new name. A variant with no file fails at chat time only. | Blocker |
| Targeting rules reference `userId`; every client that expects targeting puts `userId` on its evaluation context (.NET `EvaluationContext`, Python `eval_context`, Go `openfeature.EvaluationContext`, React provider context). | Should |
| A shape change to `flagd.json` (new top-level key, new targeting operator) is mirrored in the Go API's read/write path (`flags.go`, `targeting.go`) and its tests, because that service rewrites the file. | Blocker |
| New flag appears in the "Example feature flags in use" list of `.github/copilot-instructions.md` and in `README.md` if it documents flags. | Should |
