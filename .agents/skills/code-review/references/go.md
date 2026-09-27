# Go bucket (`src/Garage.FeatureFlags`)

Single `net/http` service: `main.go` builds the `http.Server` with `otelhttp.NewHandler(mux, "flagsapi")`; `flags.go` exposes `readFlagsFile` / `writeFlagsFile`, the only way to touch `flagd.json`, over the `flagsBackend` interface (`fileBackend` in run mode, `blobBackend` in publish mode, both in `blob_backend.go`); handlers in `handlers.go` hold `fileMutex` (`RLock` for reads, `Lock` for the read-modify-write of an update); `targeting.go` edits targeting rules; `openfeature.go` evaluates flags through OFREP. CI runs `gofmt -l .` and `go test -v ./...` and nothing else, so `go vet ./...` is worth running here when the diff touches logic.

| Check | Severity |
|---|---|
| Errors are returned or wrapped (`fmt.Errorf("...: %w", err)`), never discarded with `_`; handlers map them to a status code and log once, at the boundary. | Blocker |
| Any new access to the flags file goes through `readFlagsFile` / `writeFlagsFile` under `fileMutex`, with the update's read and write inside the same `Lock()` so two PUTs cannot interleave. | Blocker |
| A new handler is registered on the shared `mux` (so `otelhttp.NewHandler` covers it), passes `r.Context()` into `tracer.Start`, the OpenFeature evaluation and the backend call, and records failures with `span.RecordError` + `span.SetStatus(codes.Error, ...)` like the existing functions. | Should |
| A new endpoint the browser calls is reachable through `Garage.Web/default.conf.template` `location /flags/` (nginx has no dev-server counterpart for `/flags`; check `vite.config.ts` too). | Should |
| Behaviour change in `handlers.go`, `targeting.go`, or `flags.go` lands with a table-driven case in the sibling `*_test.go` (`handlers_test.go`, `targeting_test.go`, `flags_io_test.go`); handler tests use `httptest`. | Should |
| `fileBackend` and `blobBackend` implement the same `flagsBackend` contract; a behaviour change in one without the other splits run mode from publish mode, and `blobBackend` is the one no local run exercises. | Blocker |
| The `go` directive in `go.mod`, the `golang:` tag in `flags-api.Dockerfile`, and CI's `go-version-file` agree. | Blocker |
| Exported identifiers have doc comments; unexported ones are only exported when a test in another package needs them. | Nit |
