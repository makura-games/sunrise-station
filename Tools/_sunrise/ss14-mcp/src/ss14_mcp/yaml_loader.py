"""Safe YAML loader supporting SS14 !type:* and partial-patch tags."""

from __future__ import annotations

import re
from typing import Any

import yaml

_BaseLoader = getattr(yaml, "CSafeLoader", yaml.SafeLoader)
USING_C_LOADER: bool = _BaseLoader is not yaml.SafeLoader

_YAML12_BOOL_RE = re.compile(r"^(?:true|True|TRUE|false|False|FALSE)$")


class SS14SafeLoader(_BaseLoader):
    """YAML SafeLoader extended with YAML 1.2 booleans and SS14 custom tag constructors."""


# Override YAML 1.1 bool resolver so bare on/off/yes/no stay strings (matches YamlDotNet in SS14).
SS14SafeLoader.yaml_implicit_resolvers = {
    key: [
        (tag, regexp)
        for tag, regexp in resolvers
        if tag != "tag:yaml.org,2002:bool"
    ]
    for key, resolvers in _BaseLoader.yaml_implicit_resolvers.items()
}
for _bool_lead in ("t", "T", "f", "F"):
    SS14SafeLoader.yaml_implicit_resolvers.setdefault(_bool_lead, []).append(
        ("tag:yaml.org,2002:bool", _YAML12_BOOL_RE)
    )


def _construct_node_value(loader: yaml.SafeLoader, node: yaml.Node) -> Any:
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node, deep=True)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    if isinstance(node, yaml.ScalarNode):
        val = loader.construct_scalar(node)
        return None if val == "" else val
    return None


def _construct_type_tag(loader: yaml.SafeLoader, suffix: str, node: yaml.Node) -> Any:
    if isinstance(node, yaml.MappingNode):
        data = loader.construct_mapping(node, deep=True)
        data["__tag__"] = suffix
        return data
    if isinstance(node, yaml.SequenceNode):
        items = loader.construct_sequence(node, deep=True)
        return {"__tag__": suffix, "__items__": items}
    if isinstance(node, yaml.ScalarNode):
        val = loader.construct_scalar(node)
        if val == "":
            return {"__tag__": suffix}
        return {"__tag__": suffix, "__value__": val}
    return {"__tag__": suffix}


def _construct_partial_only(loader: yaml.SafeLoader, node: yaml.Node) -> Any:
    val = _construct_node_value(loader, node)
    return {"__partial_only__": True, "__value__": val}


def _construct_remove(loader: yaml.SafeLoader, node: yaml.Node) -> Any:
    val = _construct_node_value(loader, node)
    return {"__op__": "remove", "__value__": val}


def _construct_clear(loader: yaml.SafeLoader, node: yaml.Node) -> Any:
    val = _construct_node_value(loader, node)
    return {"__op__": "clear", "__value__": val}


def _construct_index_tag(loader: yaml.SafeLoader, suffix: str, node: yaml.Node) -> Any:
    try:
        idx: int | str = int(suffix)
    except ValueError:
        idx = suffix
    val = _construct_node_value(loader, node)
    return {"__op__": "index", "__index__": idx, "__value__": val}


def _construct_fallback_tag(loader: yaml.SafeLoader, tag: str, node: yaml.Node) -> Any:
    # Preserve unknown !<tag> values without failing SafeLoader.
    if isinstance(node, yaml.MappingNode):
        data = loader.construct_mapping(node, deep=True)
        data["__tag__"] = tag
        return data
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    if isinstance(node, yaml.ScalarNode):
        return loader.construct_scalar(node)
    return None


SS14SafeLoader.add_multi_constructor("!type:", _construct_type_tag)
SS14SafeLoader.add_constructor("!PartialOnly", _construct_partial_only)
SS14SafeLoader.add_constructor("!Remove", _construct_remove)
SS14SafeLoader.add_constructor("!Clear", _construct_clear)
SS14SafeLoader.add_multi_constructor("!Index:", _construct_index_tag)
SS14SafeLoader.add_multi_constructor("!", _construct_fallback_tag)
SS14SafeLoader.add_multi_constructor("", _construct_fallback_tag)


def load_yaml_document(text: str) -> Any:
    """Parse a YAML document using SS14SafeLoader."""
    return yaml.load(text, Loader=SS14SafeLoader)


def load_yaml_entries_with_lines(text: str) -> list[tuple[int, Any]]:
    """Parse top-level YAML sequence/mapping entries along with 1-based start line numbers."""
    loader = SS14SafeLoader(text)
    try:
        root_node = loader.get_single_node()
        if root_node is None:
            return []
        if isinstance(root_node, yaml.SequenceNode):
            results: list[tuple[int, Any]] = []
            for child in root_node.value:
                line = child.start_mark.line + 1
                obj = loader.construct_object(child, deep=True)
                results.append((line, obj))
            return results
        line = root_node.start_mark.line + 1
        obj = loader.construct_object(root_node, deep=True)
        return [(line, obj)]
    finally:
        loader.dispose()
