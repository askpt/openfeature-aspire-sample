"""Utility module for loading and rendering GitHub Repository Prompts (.prompt.yml files)."""

import yaml
from pathlib import Path
from typing import Any


def load_prompt(prompt_name: str, prompts_dir: str = "prompts") -> dict[str, Any]:
    """Load a .prompt.yml file and return parsed content.
    
    Args:
        prompt_name: Name of the prompt (without extension)
        prompts_dir: Directory containing prompt files
        
    Returns:
        Parsed YAML content as a dictionary
        
    Raises:
        FileNotFoundError: If the prompt file doesn't exist
    """
    prompt_path = Path(prompts_dir) / f"{prompt_name}.prompt.yml"
    with open(prompt_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def render_messages(prompt: dict[str, Any], variables: dict[str, str]) -> list[dict[str, str]]:
    """Render prompt messages with variable substitution.
    
    Replaces {{variable}} placeholders in message content with provided values.
    
    Args:
        prompt: Parsed prompt dictionary containing 'messages' key
        variables: Dictionary of variable names to values for substitution
        
    Returns:
        List of message dictionaries with role and content keys
    """
    messages = []
    for msg in prompt.get('messages', []):
        content = msg['content']
        for key, value in variables.items():
            content = content.replace(f"{{{{{key}}}}}", value)
        messages.append({"role": msg['role'], "content": content})
    return messages


def get_model_parameters(prompt: dict[str, Any]) -> dict[str, Any]:
    """Extract model parameters from a prompt.
    
    Args:
        prompt: Parsed prompt dictionary
        
    Returns:
        Dictionary of model parameters (e.g., temperature)
    """
    return prompt.get('modelParameters', {})
