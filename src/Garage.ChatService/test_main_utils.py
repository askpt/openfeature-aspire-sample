"""Unit tests for utility functions in main.py.

These tests exercise ``_as_text`` and ``_build_semconv_payloads``, which are
pure utility functions with no external service dependencies.  All heavy
third-party imports are replaced with ``MagicMock`` stubs so the test can
run without installing the full service dependency stack.
"""

import importlib.util
import os
import sys
from unittest.mock import MagicMock

import pytest

_CHAT_SERVICE_DIR = os.path.dirname(os.path.abspath(__file__))

# All third-party modules imported by main.py at the module level.
_MOCK_MODULE_NAMES = [
    "grpc",
    "fastapi",
    "fastapi.middleware",
    "fastapi.middleware.cors",
    "pydantic",
    "openai",
    "openfeature",
    "openfeature.api",
    "openfeature.contrib",
    "openfeature.contrib.provider",
    "openfeature.contrib.provider.ofrep",
    "openfeature.evaluation_context",
    "opentelemetry",
    "opentelemetry._logs",
    "opentelemetry.sdk",
    "opentelemetry.sdk.trace",
    "opentelemetry.sdk.trace.export",
    "opentelemetry.sdk.metrics",
    "opentelemetry.sdk.metrics.export",
    "opentelemetry.sdk._logs",
    "opentelemetry.sdk._logs.export",
    "opentelemetry.sdk.resources",
    "opentelemetry.instrumentation",
    "opentelemetry.instrumentation.fastapi",
    "opentelemetry.instrumentation.logging",
    "opentelemetry.trace",
    "opentelemetry.semconv",
    "opentelemetry.semconv.gen_ai",
]


@pytest.fixture(scope="module")
def main_module():
    """Import main.py with all heavy service dependencies replaced by stubs."""
    # Snapshot sys.modules so we can restore it afterwards.
    original = {name: sys.modules.get(name) for name in _MOCK_MODULE_NAMES}

    for name in _MOCK_MODULE_NAMES:
        sys.modules[name] = MagicMock()

    # Ensure prompt_loader (in the same directory) is importable.
    added_to_path = False
    if _CHAT_SERVICE_DIR not in sys.path:
        sys.path.insert(0, _CHAT_SERVICE_DIR)
        added_to_path = True

    # Remove any previously cached version so we get a fresh import.
    sys.modules.pop("main", None)

    try:
        spec = importlib.util.spec_from_file_location(
            "main", os.path.join(_CHAT_SERVICE_DIR, "main.py")
        )
        mod = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(mod)  # type: ignore[union-attr]
    finally:
        # Restore sys.modules regardless of import outcome.
        for name, orig in original.items():
            if orig is None:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = orig
        if added_to_path:
            sys.path.remove(_CHAT_SERVICE_DIR)

    return mod


# ---------------------------------------------------------------------------
# _as_text
# ---------------------------------------------------------------------------


class TestAsText:
    def test_none_returns_empty_string(self, main_module):
        assert main_module._as_text(None) == ""

    def test_string_is_returned_unchanged(self, main_module):
        assert main_module._as_text("hello") == "hello"

    def test_empty_string_returned_unchanged(self, main_module):
        assert main_module._as_text("") == ""

    def test_dict_serialised_as_json(self, main_module):
        import json

        result = main_module._as_text({"key": "value"})
        assert json.loads(result) == {"key": "value"}

    def test_list_serialised_as_json(self, main_module):
        import json

        result = main_module._as_text([1, 2, 3])
        assert json.loads(result) == [1, 2, 3]

    def test_int_serialised_as_json(self, main_module):
        assert main_module._as_text(42) == "42"

    def test_bool_serialised_as_json(self, main_module):
        assert main_module._as_text(True) == "true"
        assert main_module._as_text(False) == "false"

    def test_nested_structure_serialised_as_json(self, main_module):
        import json

        data = {"a": {"b": [1, 2]}}
        assert json.loads(main_module._as_text(data)) == data


# ---------------------------------------------------------------------------
# _build_semconv_payloads
# ---------------------------------------------------------------------------


class TestBuildSemconvPayloads:
    def test_empty_messages_returns_empty_lists(self, main_module):
        instructions, inputs = main_module._build_semconv_payloads([])
        assert instructions == []
        assert inputs == []

    def test_system_message_added_to_instructions(self, main_module):
        messages = [{"role": "system", "content": "You are helpful."}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert instructions == [{"type": "text", "content": "You are helpful."}]
        assert inputs == []

    def test_empty_system_message_not_added_to_instructions(self, main_module):
        messages = [{"role": "system", "content": ""}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert instructions == []
        assert inputs == []

    def test_system_message_with_none_content_not_added(self, main_module):
        messages = [{"role": "system", "content": None}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert instructions == []

    def test_user_message_added_to_inputs(self, main_module):
        messages = [{"role": "user", "content": "Hello"}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert instructions == []
        assert len(inputs) == 1
        assert inputs[0]["role"] == "user"
        assert inputs[0]["parts"] == [{"type": "text", "content": "Hello"}]

    def test_assistant_message_added_to_inputs(self, main_module):
        messages = [{"role": "assistant", "content": "Hi there!"}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(inputs) == 1
        assert inputs[0]["role"] == "assistant"

    def test_message_without_role_defaults_to_user(self, main_module):
        messages = [{"content": "No role here"}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(inputs) == 1
        assert inputs[0]["role"] == "user"

    def test_message_without_content_not_added_when_no_tool_calls(self, main_module):
        # _as_text(None) == "" which is falsy, so no text part; no tool_calls
        # either → parts is empty → message is skipped.
        messages = [{"role": "user"}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert inputs == []

    def test_mixed_messages_split_correctly(self, main_module):
        messages = [
            {"role": "system", "content": "System prompt"},
            {"role": "user", "content": "Question"},
            {"role": "assistant", "content": "Answer"},
        ]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(instructions) == 1
        assert instructions[0]["content"] == "System prompt"
        assert len(inputs) == 2
        assert inputs[0]["role"] == "user"
        assert inputs[1]["role"] == "assistant"

    def test_tool_calls_included_as_parts(self, main_module):
        messages = [
            {
                "role": "assistant",
                "content": None,
                "tool_calls": [
                    {
                        "id": "call_1",
                        "function": {
                            "name": "get_weather",
                            "arguments": '{"city": "Paris"}',
                        },
                    }
                ],
            }
        ]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(inputs) == 1
        tool_parts = [p for p in inputs[0]["parts"] if p.get("type") == "tool_call"]
        assert len(tool_parts) == 1
        assert tool_parts[0]["id"] == "call_1"
        assert tool_parts[0]["name"] == "get_weather"
        assert tool_parts[0]["arguments"] == '{"city": "Paris"}'

    def test_tool_call_with_none_tool_calls_treated_as_empty(self, main_module):
        # `None or []` evaluates to [], so no tool_call parts are added.
        messages = [{"role": "assistant", "content": "Hi", "tool_calls": None}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(inputs) == 1
        assert inputs[0]["parts"] == [{"type": "text", "content": "Hi"}]

    def test_non_string_content_serialised_via_as_text(self, main_module):
        import json

        messages = [{"role": "user", "content": {"text": "complex"}}]
        instructions, inputs = main_module._build_semconv_payloads(messages)
        assert len(inputs) == 1
        content_text = inputs[0]["parts"][0]["content"]
        assert json.loads(content_text) == {"text": "complex"}

    def test_multiple_tool_calls_all_included(self, main_module):
        messages = [
            {
                "role": "assistant",
                "content": None,
                "tool_calls": [
                    {"id": "c1", "function": {"name": "fn1", "arguments": "{}"}},
                    {"id": "c2", "function": {"name": "fn2", "arguments": "{}"}},
                ],
            }
        ]
        _, inputs = main_module._build_semconv_payloads(messages)
        tool_parts = [p for p in inputs[0]["parts"] if p.get("type") == "tool_call"]
        assert len(tool_parts) == 2
        assert {p["name"] for p in tool_parts} == {"fn1", "fn2"}

    def test_message_with_content_and_tool_calls_has_both_parts(self, main_module):
        messages = [
            {
                "role": "assistant",
                "content": "Here is the result:",
                "tool_calls": [
                    {"id": "c1", "function": {"name": "fn", "arguments": "{}"}}
                ],
            }
        ]
        _, inputs = main_module._build_semconv_payloads(messages)
        parts = inputs[0]["parts"]
        assert any(p["type"] == "text" for p in parts)
        assert any(p["type"] == "tool_call" for p in parts)
