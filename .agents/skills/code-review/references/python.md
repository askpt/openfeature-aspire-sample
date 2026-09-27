# Python bucket (`src/Garage.ChatService`)

FastAPI on Uvicorn, `uv`-managed with a frozen `uv.lock`. `main.py` holds the app and the `/chat` endpoint (called by the browser as `/api/chat`), `prompt_loader.py` loads `prompts/*.prompt.yml`. CI runs only `uv sync --frozen --group dev` and `uv run pytest`; `black` (line length 100) and `mypy` are documented standards with no CI enforcement, so Step 4 runs `uvx black --check .` and this bucket reports type-hint gaps.

| Check | Severity |
|---|---|
| `pyproject.toml` dependency change without the matching `uv.lock` change; `uv sync --frozen` refuses to install. | Blocker |
| Blocking call inside an `async def` endpoint (sync `OpenAI` client, `requests`, `time.sleep`, file IO on the request path). The model client is `AsyncOpenAI`; a new call awaits it. | Blocker |
| A `try` around the model call catches `openai.APIError` (plus the explicit empty-`choices` `ValueError`), calls `model_span.record_exception(e)` and `model_span.set_status(Status(StatusCode.ERROR))`, and counts `status="fallback"`; a bare `except Exception` there turns our own bugs into a 200 with `status="fallback"` and a green span, so anything else propagates to the outer handler (`status="error"`, 500). | Should |
| User-supplied `message` reaches only the `{{message}}` placeholder of the user turn in the `.prompt.yml`; nothing from the request is formatted into the `system` message or into the prompt filename lookup (`prompt-file` values are trusted flag variants, not request input). | Blocker |
| `CHAT_MODEL_KEY` is never logged, echoed in an error body, or added to a span attribute; new config is read from an env var that `Garage.AppHost/Program.cs` sets, with a documented default. | Blocker |
| Flag evaluation passes the `eval_context` carrying `userId`; `enable-chatbot` and `prompt-file` follow that pattern. | Should |
| New span or metric follows the existing names (`chat_requests_total`, `chat_request_duration_seconds`: snake_case, unit suffix) and the OTLP gRPC exporter already configured. | Should |
| New behaviour has a `test_*.py` beside the module, mocking the flag client and model client as `test_main_utils.py` does; tests stay hermetic (no network, no `.venv` state). | Should |
| New functions carry type hints (`check_untyped_defs` is on); `Optional` handled at the boundary, not with `# type: ignore`. | Should |
| A new `prompts/<name>.prompt.yml` follows the GitHub Repository Prompts shape (`name`, `description`, `model`, `modelParameters`, `messages` with `system` + `user`) and is registered as a `prompt-file` variant (see `flags.md`). | Blocker |
