"""Unit tests for prompt_loader module."""

import sys
from pathlib import Path

import pytest

# Ensure the directory containing this test (and prompt_loader.py) is on sys.path
_THIS_DIR = Path(__file__).resolve().parent
if str(_THIS_DIR) not in sys.path:
    sys.path.insert(0, str(_THIS_DIR))
from prompt_loader import load_prompt, render_messages, get_model_parameters


class TestRenderMessages:
    def test_substitutes_single_variable(self):
        prompt = {"messages": [{"role": "user", "content": "Hello {{name}}!"}]}
        result = render_messages(prompt, {"name": "World"})
        assert result == [{"role": "user", "content": "Hello World!"}]

    def test_substitutes_multiple_variables(self):
        prompt = {
            "messages": [
                {"role": "system", "content": "You are {{persona}}."},
                {"role": "user", "content": "{{message}}"},
            ]
        }
        result = render_messages(prompt, {"persona": "an expert", "message": "Tell me more"})
        assert result[0]["content"] == "You are an expert."
        assert result[1]["content"] == "Tell me more"

    def test_leaves_unmatched_placeholder_unchanged(self):
        prompt = {"messages": [{"role": "user", "content": "Hello {{unknown}}!"}]}
        result = render_messages(prompt, {"name": "World"})
        assert result == [{"role": "user", "content": "Hello {{unknown}}!"}]

    def test_empty_messages_returns_empty_list(self):
        assert render_messages({}, {}) == []
        assert render_messages({"messages": []}, {}) == []

    def test_preserves_message_roles(self):
        prompt = {
            "messages": [
                {"role": "system", "content": "System prompt"},
                {"role": "user", "content": "User {{msg}}"},
                {"role": "assistant", "content": "Assistant response"},
            ]
        }
        result = render_messages(prompt, {"msg": "question"})
        assert [m["role"] for m in result] == ["system", "user", "assistant"]

    def test_returns_new_list_not_mutation(self):
        original = [{"role": "user", "content": "Hello {{x}}"}]
        prompt = {"messages": original}
        result = render_messages(prompt, {"x": "test"})
        assert result is not prompt["messages"]
        assert original[0]["content"] == "Hello {{x}}"


class TestGetModelParameters:
    def test_returns_model_parameters(self):
        prompt = {"modelParameters": {"temperature": 0.7, "max_tokens": 100}}
        result = get_model_parameters(prompt)
        assert result == {"temperature": 0.7, "max_tokens": 100}

    def test_returns_empty_dict_when_missing(self):
        assert get_model_parameters({}) == {}
        assert get_model_parameters({"messages": []}) == {}

    def test_returns_empty_dict_for_empty_parameters(self):
        assert get_model_parameters({"modelParameters": {}}) == {}


class TestLoadPrompt:
    def test_loads_valid_prompt_file(self, tmp_path: Path):
        prompt_dir = tmp_path / "prompts"
        prompt_dir.mkdir()
        prompt_file = prompt_dir / "test.prompt.yml"
        prompt_file.write_text(
            "name: Test Prompt\n"
            "messages:\n"
            "  - role: user\n"
            "    content: Hello {{message}}\n"
            "modelParameters:\n"
            "  temperature: 0.5\n",
            encoding="utf-8",
        )
        result = load_prompt("test", str(prompt_dir))
        assert result["name"] == "Test Prompt"
        assert result["modelParameters"]["temperature"] == 0.5
        assert result["messages"][0]["role"] == "user"

    def test_raises_file_not_found_for_missing_prompt(self, tmp_path: Path):
        with pytest.raises(FileNotFoundError):
            load_prompt("nonexistent", str(tmp_path))

    def test_loads_prompt_with_default_directory(self, tmp_path: Path):
        """Verify load_prompt uses the prompts_dir parameter correctly."""
        prompt_dir = tmp_path / "prompts"
        prompt_dir.mkdir()
        (prompt_dir / "simple.prompt.yml").write_text(
            "name: Simple\nmessages: []\n", encoding="utf-8"
        )
        result = load_prompt("simple", str(prompt_dir))
        assert result["name"] == "Simple"
