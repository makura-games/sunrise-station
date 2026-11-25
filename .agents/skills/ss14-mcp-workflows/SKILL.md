---
name: ss14-mcp-workflows
description: Practical guide to using Rider MCP, ss14-mcp, and ss13-dm-mcp together in sunrise-station: tool selection matrix, exact arguments, SS13-to-SS14 porting workflow, and pre-PR audit checklists.
---

# MCP-воркфлоу для `sunrise-station`: Rider MCP, `ss14-mcp`, `ss13-dm-mcp`

## Правила доступности и fallback

- Используй инструменты каждого MCP **только если этот MCP-сервер подключён в текущей сессии**.
- Если опциональный сервер (`ss13-dm-mcp` или `ss14-mcp`) не подключён — спокойно используй обычные инструменты чтения/поиска; не пытайся устанавливать, конфигурировать или запускать MCP-серверы без явной просьбы пользователя.
- Никогда не хардкодь локальные пути машины разработчика в код, тесты или документацию.

## Матрица выбора инструментов

| Задача | Предпочтительный MCP-инструмент |
| :--- | :--- |
| Поиск C#-символов, ссылок, файлов в решении | **Rider MCP**: `search_symbol`, `search_text`, `search_regex`, `search_file`, `list_directory_tree` |
| Чтение/редактирование C#, инспекции и ошибки IDE | **Rider MCP**: `read_file`, `get_symbol_info`, `get_file_problems`, `replace_text_in_file`, `rename_refactoring`, `reformat_file` |
| Итоговый YAML-прототип с `parent`, `_Sunrise/Partial` и затиранием списков | **`ss14-mcp`**: `ss14_resolve_prototype` |
| Поиск прототипов по компоненту, `Tag`, родителю или префиксу пути | **`ss14-mcp`**: `ss14_query_prototypes` |
| Дерево предков/потомков прототипа (что затронет правка) | **`ss14-mcp`**: `ss14_get_inheritance_tree` |
| Быстрая выжимка `[DataField]` и сетевых атрибутов `*Component.cs` | **`ss14-mcp`**: `ss14_inspect_component` |
| Проверка `.rsi` (`meta.json`, PNG, дубли кадров, звуки, `attributions.yml`) | **`ss14-mcp`**: `ss14_audit_rsi` |
| Паритет локализации `ru-RU ↔ en-US`, `ent-*` и `Loc.GetString` | **`ss14-mcp`**: `ss14_audit_ftl` |
| Аудит `git diff` вне `_Sunrise` и маркеров `Sunrise-Edit` по `AGENTS.md` | **`ss14-mcp`**: `ss14_check_upstream_hygiene` |
| Инспекция DM-типа SS13 (файлы, `var`, сигнатуры `proc`/`verb`) | **`ss13-dm-mcp`**: `dm_get_type` |
| Исходный код DM-процедуры и цепочки переопределений `..()` | **`ss13-dm-mcp`**: `dm_get_proc` |
| Поиск `#define`-макросов SS13 | **`ss13-dm-mcp`**: `dm_search_defines` |
| Regex-поиск по `.dm` с enclosing type/proc и контекстом | **`ss13-dm-mcp`**: `dm_search_code` |
| Метаданные `.dmi` (`zTXt`) и SHA256-хеши стейтов/кадров | **`ss13-dm-mcp`**: `dmi_inspect` |

## Справочник сигнатур

### Rider MCP
- **Разрешено:** `search_symbol`, `search_text`, `search_regex`, `search_file`, `list_directory_tree`, `read_file`, `get_symbol_info`, `get_file_problems`, `replace_text_in_file`, `rename_refactoring`, `reformat_file`.
- **Запрещено:** `execute_terminal_command`, `execute_run_configuration`, `get_run_configurations`, `get_project_modules`, `get_project_dependencies`.

### `ss14-mcp` (7 инструментов, `Tools/_sunrise/ss14-mcp`)
- `ss14_resolve_prototype(proto_id, proto_type="entity", components=None)`
- `ss14_query_prototypes(component=None, tag=None, parent=None, id_contains=None, path_prefix=None, include_abstract=False, limit=30)`
- `ss14_get_inheritance_tree(proto_id, direction="both", max_depth=5, proto_type="entity")`
- `ss14_inspect_component(component_name)`
- `ss14_audit_rsi(rsi_path, check_attributions=True, audio_paths=None)`
- `ss14_audit_ftl(path_prefix, proto_path_prefix=None, cs_path_prefix=None, limit=50)`
- `ss14_check_upstream_hygiene(base_ref=None)`

### `ss13-dm-mcp` (5 инструментов, опциональный локальный сервер)
- `dm_get_type(typepath, include_inherited=True, filter_prefix=None, repo=None)`
- `dm_get_proc(typepath, proc_name, include_parents=False, repo=None)`
- `dm_search_defines(query, limit=30, repo=None)`
- `dm_search_code(pattern, subpath=None, limit=30, repo=None)`
- `dmi_inspect(dmi_path, state=None, include_frame_hashes=False, repo=None)`

> Если в `ss13-dm-mcp` через `SS13_REPOS` настроено несколько билдов SS13, передавай имя билда в необязательный параметр `repo`.

## Типовые сценарии работы

### 1. Повседневная разработка и рефакторинг в `sunrise-station`
1. **Изучение прототипов:** перед правкой базового прототипа вызови `ss14_resolve_prototype` и `ss14_get_inheritance_tree(direction="down")`, чтобы увидеть унаследованные компоненты и затронутые дочерние сущности. Обращай внимание на `warnings` о затирании списков родителя (`Tag.tags`, `Whitelist.tags`).
2. **Работа с C#-компонентами:** используй `ss14_inspect_component`, чтобы быстро получить `[DataField]`, типы и дефолты без чтения всего файла; код редактируй через Rider MCP и проверяй через `get_file_problems`.
3. **Пред-PR чеклист:**
   - при добавлении/правке спрайтов и звуков — `ss14_audit_rsi(..., check_attributions=True)`;
   - при добавлении/правке локализации — `ss14_audit_ftl(path_prefix=...)`;
   - перед завершением задачи — `ss14_check_upstream_hygiene()` для проверки маркеров `Sunrise-Edit` вне `_Sunrise`.

### 2. Портирование фичи из SS13 в SS14 (`ss13-dm-mcp` + `ss14-mcp`)
1. **Разбор логики SS13:** начни с `dm_get_type(typepath)` для обзора переменных и сигнатур, затем читай только нужные процедуры через `dm_get_proc(..., include_parents=True)` и константы через `dm_search_defines`.
2. **Сверка графики `.dmi` ↔ `.rsi`:** вызови `dmi_inspect(dmi_path, state=..., include_frame_hashes=True)` в `ss13-dm-mcp` и сравни число кадров, задержки (`delay`) и уникальные SHA256-хеши кадров с `ss14_audit_rsi(rsi_path)` в `ss14-mcp` (чтобы не пропустить дублирующиеся или статичные кадры).
3. **Реализация и аудит в SS14:** реализуй фичу в `_Sunrise` с помощью Rider MCP и закрой задачу пред-PR проверками (`ss14_audit_rsi`, `ss14_audit_ftl`, `ss14_check_upstream_hygiene`).
