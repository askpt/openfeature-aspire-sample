# Web bucket (`src/Garage.Web`)

React 19 + TypeScript strict + Vite. `npm run build` is `tsc -b && vite build`, so type errors fail CI; `npm run lint` is ESLint. The browser reaches backends only through relative paths that two proxies must both know: `vite.config.ts` `server.proxy` in dev (`/api/chat` → chatservice, `/api` → apiservice, `/ofrep` → flagd) and `default.conf.template` (nginx) in the container (`/api/chat`, `/api/`, `/flags/`, `/ofrep/`). There are no tests today.

| Check | Severity |
|---|---|
| A new backend call uses a relative path that exists in **both** `vite.config.ts` and `default.conf.template`, with the same rewrite (`/api/x` → `/x`). Missing from one → works locally, 404s in the container, or the reverse. | Blocker |
| Flag hooks (`useBooleanFlagValue`, `useStringFlagValue`, `useNumberFlagValue`) render inside `<OpenFeatureProvider>` (`App.tsx`) and the key exists in `flagd.json` (see `flags.md`); the provider is `OFREPWebProvider` set once in `featureFlags.ts`. | Blocker |
| `fetch` on mount is cancelled on unmount (`AbortController` + `signal`, as `Home.tsx` does) and errors set state instead of throwing into the render. | Should |
| Runtime configuration comes only from the `import.meta.env.VITE_*` keys that `vite.config.ts` defines from Aspire env vars (`OFREP_ENDPOINT`, `OTEL_EXPORTER_OTLP_*`); a new key is defined there and in the nginx template, never hard-coded. | Blocker |
| Rendering user- or model-provided text uses React text nodes, not `dangerouslySetInnerHTML`; chat responses are strings, not HTML. | Blocker |
| A new `@opentelemetry/*` import is on the same version line as its neighbours (`2.x` stable, `0.x` experimental) and is instantiated in `main.tsx` once. | Should |
| Component logic that branches on state or flags has no test; suggest Vitest, report as Should since no runner exists yet. | Should |
| Deep render-performance review (memoisation, effects, data fetching): `.agents/skills/vercel-react-best-practices` when the diff is UI-heavy. | — |
