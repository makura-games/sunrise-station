# Repository Agent Instructions

This file is the short entrypoint for agents working in this repository. Keep detailed operating rules in `.agents/rules` and detailed skills in `.agents/skills`.

Source-of-truth locations:

- Rules: `.agents/rules`
- Skills: `.agents/skills`
- Compatibility bridges: `.agent`, `.claude`, `.cursor`, `.github`

At the start of a new dialogue, and again after any context compaction, reload the always-on rules from `.agents/rules` and review the available skills in `.agents/skills`. Do not repeat that full reload for every later chat message unless the task changes enough to require new skills or rules.

Before planning, analyzing, or editing, select the rules and skills that apply to the current task. Prefer file extensions and touched subsystems over loose keyword matching. For example, `.cs`, `.yml`, `.yaml`, `.ftl`, and `.swsl` files have explicit SS14 guidance in the rules and skills.

Useful entrypoints:

- Skill and rule preflight: `.agents/rules/ss14-skill-preflight-and-refresh.md`
- Testing requirements: `.agents/rules/ss14-testing-guidelines.md`
- Codebase prefix and edit markers: `.agents/rules/ss14-codebase-prefix-detection.md`
- Interaction architecture pattern: `.agents/rules/ss14-interaction-flow.md`
- Rule authoring policy: `.agents/rules/AUTHORING_POLICY.md`
- Skill authoring policy: `.agents/skills/AUTHORING_POLICY.md`


**Остальное - если связано с темой**
Пример запроса: Нужно добавить систему для проигрывания звука при спавне сущности X

Нужные скилы(по категориям)

Из всегда:
ss14-naming-conventions
ss14-ecs-prototypes
ss14-upstream-maintenance

Так как нужен сишарп код

**Если требуется писать C# код**
ss14-ecs-components
ss14-ecs-entities
ss14-ecs-prototypes
ss14-ecs-systems
ss14-events
ss14-prediction

Система маленькая - не берем оптимизации
Нет требуется перевод - не берем перевод

Требуется работа со звуком - находим скилл содержащий слово audio
ss14-audio-system-api - берем
ss14-audio-system-core - слишком продвинуто, не нужно в таком задаче - не берем.

---

Если задача составить план - включи в план скилы, которые требуются для выполнении задачи используя этот гайд
Если выполняется автоматическая чистка/компрессия контекста - перечитай все скилы и правила после нее каждый раз.

Если делаешь большую исследовательскую работу/большой код по плану - заведи временный файл для записывания всех важных мыслей или деталей.
Записывай их туда, чтобы не терять во время чисток/компрессия контекста.
Удаляй после

## Язык комментариев и словарь

- Поясняющую часть inline-комментариев, комментариев у `using` и reason-фраз у маркеров `Sunrise edit`, `Sunrise added`, `Sunrise edit start/end`, `Sunrise added start/end` писать по-русски.
- Английские идентификаторы, имена типов, API, loc-key, canonical markers и названия C#-символов не переводить.
- В prose-описаниях рядом с кодом для `Sponsor*` использовать термин **«спонсор»**.
- `donor` и `donator` считать нежелательной терминологией для новых комментариев, reason-фраз и поясняющих описаний.

## RIDER MCP

Всегда проверяй доступен ли rider mcp. Если он доступен делай следующее
1. Используй rider mcp для проверки файлов, которые ты изменил.
    1. Всегда делай предложения и исправляй ошибки, которые пишет rider. Исключение: то, что напрямую противоречит логике программы. Пример: предложение сделать приватный метод, который задуман как public API, но еще не имеет использований.
2. Используй rider mcp инструменты для других задач, чтобы упростить свою работу.

При работе в этом репозитории приоритетно используй Rider MCP вместо shell везде, где есть эквивалент.

Порядок предпочтения:
1. Поиск и навигация: search_symbol, search_text, search_regex, search_file, list_directory_tree.
2. Чтение и анализ: read_file, get_symbol_info, get_file_problems.
3. Правки: replace_text_in_file, rename_refactoring, reformat_file.

Запрещено использовать:
- execute_terminal_command
- execute_run_configuration
- get_run_configurations
- get_project_modules
- get_project_dependencies

ВСЕГДА ИСПОЛЬЗУЙ RIDER MCP ДЛЯ РАБОТЫ СО ВСЕМ, ЕСЛИ ОН ЕСТЬ. НИКОГДА НЕ ИСПОЛЬЗУЙ SHELL И ЕГО КОМАНДЫ ПРИ НАЛИЧИИ РАЗРЕШЕННЫХ RIDER MCP КОМАНД АНАЛОГОВ!!!

## ТЕСТИРОВАНИЕ

В конце работы над кодом провести тестирование

Если код затрагивает прототипы(YAML/FTL) -> запускать проект `Content.YAMLLinter` командой `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build`; если линтер ещё не собран, сначала `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Release --no-restore /m`
Если код затрагивает C# -> билдить измененный проект
Если код затрагивает клиент - запускать клиент командой `dotnet run --project Content.Client/Content.Client.csproj` или `dotnet run --project Content.Client/Content.Client.csproj --configuration Tools`, чтобы проверить runtime ошибки и IL verification.

Использовать `dotnet` по конкретному сценарию:
1. Компиляция изменённого проекта: `dotnet build <relative/path/to/project.csproj> --configuration Debug`; для CI/жёсткой проверки использовать `dotnet build <relative/path/to/project.csproj> --configuration Release --no-restore /m`
2. Запуск всех тестов решения: `dotnet test SpaceStation14.slnx --configuration DebugOpt --no-build`
3. Запуск конкретного test project: `dotnet test Content.Tests/Content.Tests.csproj --configuration DebugOpt --no-build` или `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build`
4. Запуск конкретного теста: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --filter "FullyQualifiedName~GravityGridTest"`
5. Локальный запуск клиента: `dotnet run --project Content.Client/Content.Client.csproj` или `dotnet run --project Content.Client/Content.Client.csproj --configuration Tools`
6. Сборка publish-артефактов при необходимости: `dotnet publish Content.Packaging/Content.Packaging.csproj --configuration Release -r win-x64` или `dotnet publish Content.Packaging/Content.Packaging.csproj --configuration Release -p:PublishProfile=<ProfileName>`

Никогда не запускай больше двух тестов одновременно, чтобы не привести к лагам на компьютере пользователя. Идеально - по одному тесту за раз.

После обязательно завершить начатый процесс в системе!
