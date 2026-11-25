# ss14-mcp

Командный **read-only** Python MCP-сервер для повседневной разработки в `sunrise-station` (Windows / Linux / WSL).

> **Примечание о регистре папки:** в репозитории `sunrise-station` папка `Tools/_sunrise` уже отслеживается в Git в нижнем регистре, поэтому сервер размещён в `Tools/_sunrise/ss14-mcp` (на Windows NTFS пути `Tools/_Sunrise/...` и `Tools/_sunrise/...` эквивалентны; на Linux/WSL используйте `Tools/_sunrise/ss14-mcp`).

## Возможности (7 MCP-инструментов)

1. `ss14_resolve_prototype` — склейка YAML-прототипа по цепочке `parent` (включая множественное наследование) и патчам `_Sunrise/Partial`, с источниками (`file:line`), финальными компонентами и предупреждениями о затирании списков родителя (например, `Tag.tags`).
2. `ss14_query_prototypes` — поиск сущностей по унаследованному компоненту, тегу `Tag`, базовому родителю, подстроке `id` или префиксу пути (с исключением `abstract: true` по умолчанию).
3. `ss14_get_inheritance_tree` — дерево предков (`up`), потомков (`down`) или обоих направлений (`both`).
4. `ss14_inspect_component` — быстрая выжимка `[DataField]` из `*Component.cs` (YAML-имя, C#-тип, дефолт, `[AutoNetworkedField]`, `[AutoPausedField]`, `[ViewVariables]`, `partial class` объединяются).
5. `ss14_audit_rsi` — проверка `.rsi/meta.json` против PNG на диске (размеры, кадры/направления, SHA256, дубли PNG и попиксельные заглушки), проверка звуков и покрытия ближайшим `attributions.yml`.
6. `ss14_audit_ftl` — паритет ключей `ru-RU ↔ en-US`, поиск прототипов без `ent-*` и проверка `Loc.GetString("...")` из C#.
7. `ss14_check_upstream_hygiene` — read-only аудит `git status` / `git diff` (или против `base_ref`) на изменения вне `_Sunrise` и наличие маркеров `Sunrise-Edit` по `AGENTS.md`.

## Автоподключение из репозитория (out-of-the-box)

После `git clone` достаточно иметь установленный [`uv`](https://docs.astral.sh/uv/) — проектные конфиги уже лежат в репозитории и не требуют ручной правки глобального `mcp_config`:

- **Antigravity:** workspace-плагин `.agents/plugins/ss14-sunrise/` (`plugin.json` + `mcp_config.json`, включён по умолчанию).
- **Codex (CLI / IDE):** `.codex/config.toml` (подхватывается автоматически при запуске из корня доверенного репозитория).
- **Claude Code:** `.mcp.json` в корне репозитория.
- **Cursor:** `.cursor/mcp.json`.

Если `uv` не установлен или нужно подключить сервер вручную/глобально, см. варианты ниже.

## Zero-config и кэш

- Корень репозитория определяется автоматически: сначала по `SS14_REPO_PATH` (если задан), затем от текущей рабочей директории (`CWD`) вверх и с fallback от `__file__` вверх (ищет `Resources/Prototypes` и `Content.Server`).
- Все индексы ленивые (старт сервера < 0.3 с) и автоматически обновляются инкрементально по `mtime` при изменении файлов — перезапуск MCP не нужен.

## Установка и запуск

### Вариант 1: глобальная editable-установка в `PATH` (без абсолютных путей в `mcp_config.json`)

```bash
uv pip install --system -e Tools/_sunrise/ss14-mcp
```

После этого команда `ss14-mcp` доступна глобально из любого каталога (editable-ссылка на клон репозитория + автопоиск корня от `CWD` / `__file__`).

### Вариант 2: `uv` с изоляцией `.venv`

```bash
uv --directory Tools/_sunrise/ss14-mcp sync
```

### Вариант 3: стандартный `python -m venv`

**Windows (PowerShell):**
```powershell
cd Tools/_sunrise/ss14-mcp
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".[dev]"
```

**Linux / macOS / WSL:**
```bash
cd Tools/_sunrise/ss14-mcp
python3 -m venv .venv
.venv/bin/python -m pip install -e ".[dev]"
```

## Подключение в `mcp_config.json` (Antigravity / Cursor / Claude Code)

### Без абсолютных путей (после `uv pip install --system -e`)
```json
{
  "mcpServers": {
    "ss14-sunrise": {
      "command": "ss14-mcp"
    }
  }
}
```

### Через `uv --directory`
```json
{
  "mcpServers": {
    "ss14-sunrise": {
      "command": "uv",
      "args": [
        "--directory",
        "<REPO_ROOT>/Tools/_sunrise/ss14-mcp",
        "run",
        "ss14-mcp"
      ]
    }
  }
}
```

### Через `.venv` (если `uv` не установлен)
```json
{
  "mcpServers": {
    "ss14-sunrise": {
      "command": "<REPO_ROOT>/Tools/_sunrise/ss14-mcp/.venv/Scripts/python.exe",
      "args": ["-m", "ss14_mcp.server"]
    }
  }
}
```
*(На Linux/WSL замените `.venv/Scripts/python.exe` на `.venv/bin/python`.)*

## Запуск тестов

```powershell
uv --directory Tools/_sunrise/ss14-mcp run --extra dev pytest -v
```
*(Если используете `.venv` из Варианта 3: `.\.venv\Scripts\pytest.exe -v`.)*
