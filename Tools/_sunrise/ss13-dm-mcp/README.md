# ss13-dm-mcp

Read-only Python MCP-сервер для чтения кодовой базы Space Station 13 (`.dm`-файлы, `#define`, наследование типов/проков) и инспекции `.dmi`-спрайтшитов (`zTXt` + SHA256-хеши кадров).

> **Почему `ss13-dm-mcp` не включён автоматически в проектных конфигах:** серверу требуется путь `SS13_REPO_PATH` на локальную папку со сборкой SS13 (Paradise, TG и др.), которой нет внутри репозитория `sunrise-station`. Поэтому каждый разработчик подключает его в своём личном конфиге агента.

## Инструменты (5 tools)

1. `dm_get_type(typepath, include_inherited=True, filter_prefix=None, repo=None)` — все файлы/строки определения типа, итоговые `var` (с источником переопределения) и сигнатуры проков/вербов.
2. `dm_get_proc(typepath, proc_name, include_parents=False, repo=None)` — точный исходный код процедуры (или всей цепочки `..()` с `include_parents=True`).
3. `dm_search_defines(query, limit=30, repo=None)` — поиск `#define`-макросов по имени с файлом и номером строки.
4. `dm_search_code(pattern, subpath=None, limit=30, repo=None)` — regex-поиск по `.dm` с возвратом `file:line`, enclosing type/proc и 1 строкой контекста.
5. `dmi_inspect(dmi_path, state=None, include_frame_hashes=False, repo=None)` — метаданные `zTXt` (`version`, `width`, `height`, стейты `dirs`/`frames`/`delay`/`loop`/`rewind`) и опционально SHA256-хеши каждого стейта и кадра в порядке `frame-major × dir`.

## Переменные окружения

- `SS13_REPO_PATH` — путь к основному билду SS13 (например, `<PATH_TO_SS13_REPO>`).
- `SS13_REPOS` *(опционально)* — JSON-словарь для переключения между несколькими билдами через аргумент `repo`, например:
  `{"paradise": "<PATH_TO_PARADISE>", "tg": "<PATH_TO_TG>"}`.
- Индексы ленивые (старт сервера < 0.3 с) и обновляются автоматически по `mtime` при изменении файлов.

## Установка

### Вариант 1: через `uv` (рекомендуется, ничего устанавливать вручную не нужно)

При запуске `uv --directory Tools/_sunrise/ss13-dm-mcp run ss13-dm-mcp` окружение `.venv` создаётся автоматически.

### Вариант 2: глобальная editable-установка в `PATH` (без абсолютного пути к серверу)

```bash
uv tool install -e Tools/_sunrise/ss13-dm-mcp
```
*(Либо `uv pip install --system -e Tools/_sunrise/ss13-dm-mcp`, если системный Python не помечен как `EXTERNALLY-MANAGED`.)*

После этого команда `ss13-dm-mcp` доступна глобально из любого каталога.

### Вариант 3: стандартный `python -m venv` (если `uv` не установлен)

**Windows (PowerShell):**
```powershell
cd Tools/_sunrise/ss13-dm-mcp
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".[dev]"
```

**Linux / macOS / WSL:**
```bash
cd Tools/_sunrise/ss13-dm-mcp
python3 -m venv .venv
.venv/bin/python -m pip install -e ".[dev]"
```

## Подключение в агентах

### Antigravity (`~/.gemini/config/mcp_config.json`), Cursor (`~/.cursor/mcp.json`) и Claude Code

**Через `uv`:**
```json
{
  "mcpServers": {
    "ss13-dm": {
      "command": "uv",
      "args": [
        "--directory",
        "<REPO_ROOT>/Tools/_sunrise/ss13-dm-mcp",
        "run",
        "ss13-dm-mcp"
      ],
      "env": {
        "SS13_REPO_PATH": "<PATH_TO_SS13_REPO>"
      }
    }
  }
}
```

**Если установлен глобально в `PATH` (Вариант 2):**
```json
{
  "mcpServers": {
    "ss13-dm": {
      "command": "ss13-dm-mcp",
      "env": {
        "SS13_REPO_PATH": "<PATH_TO_SS13_REPO>"
      }
    }
  }
}
```

**Через `.venv` (Вариант 3, без `uv`):**
```json
{
  "mcpServers": {
    "ss13-dm": {
      "command": "<REPO_ROOT>/Tools/_sunrise/ss13-dm-mcp/.venv/Scripts/python.exe",
      "args": ["-m", "ss13_dm_mcp.server"],
      "env": {
        "SS13_REPO_PATH": "<PATH_TO_SS13_REPO>"
      }
    }
  }
}
```
*(На Linux/macOS/WSL замените `.venv/Scripts/python.exe` на `.venv/bin/python`.)*

### Codex (`~/.codex/config.toml`)

**Через `uv`:**
```toml
[mcp_servers.ss13-dm]
command = "uv"
args = ["--directory", "<REPO_ROOT>/Tools/_sunrise/ss13-dm-mcp", "run", "ss13-dm-mcp"]
env = { SS13_REPO_PATH = "<PATH_TO_SS13_REPO>" }
```

**Если установлен глобально в `PATH` (Вариант 2):**
```toml
[mcp_servers.ss13-dm]
command = "ss13-dm-mcp"
env = { SS13_REPO_PATH = "<PATH_TO_SS13_REPO>" }
```

## Запуск тестов

```powershell
# Только юнит-тесты и синтетический stdio-тест (Paradise smoke-тесты пропускаются, если не указан билд Paradise)
uv --directory Tools/_sunrise/ss13-dm-mcp run --extra dev pytest -v

# Включая smoke-тесты на локальном билде Paradise (нужны clockwork_mob.dm / cogscarab.dm / clockwork.dmi)
$env:SS13_REPO_PATH = "<PATH_TO_PARADISE_REPO>"
uv --directory Tools/_sunrise/ss13-dm-mcp run --extra dev pytest -v
```
