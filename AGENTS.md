# Repository Agent Instructions

Постоянные правила (`always_on`) для репозитория `sunrise-station`. Справочники по подсистемам (On-Demand): `.agents/skills/<skill-name>/SKILL.md` (мосты для других IDE: `.claude/skills`, `.cursor/skills`, `.github/skills`; политика: `.agents/skills/AUTHORING_POLICY.md`).

## Выбор скиллов и рабочие заметки

- **Progressive Disclosure**: не загружай все базовые скиллы подряд. Активируй только скиллы из `.agents/skills`, чьё описание (`description`) относится к затрагиваемой подсистеме или задаче.
- При составлении плана включай в него скиллы, нужные для его выполнения.
- Для крупных исследований или больших правок веди временный файл заметок (решения, пути, инварианты) и **удаляй его перед завершением задачи**.

## Форк Sunrise и маркеры правок

- Префикс кодовой базы: `Sunrise`. Папка для нового кода, `partial`-систем, прототипов и ассетов: `_Sunrise`. Префикс новых форк-ID прототипов: `Sunrise*`.
- В ванильных файлах оставлять только минимальные хуки с маркерами:
  - Однострочный: `// Sunrise-Edit - причина` (C#), `# Sunrise-Edit - причина` (YAML/FTL), `<!-- Sunrise-Edit -->` (XML/XAML).
  - Многострочный: `// Sunrise edit start - причина` … `// Sunrise edit end` или `// Sunrise added start - причина` … `// Sunrise added end` (в YAML/FTL с `#`).
- При правке файлов внутри унаследованных модулей (`_Starlight`, `_RMC14`, `_Fish`, `_Afterlight`, `_FarHorizons`) сохранять их локальную структуру, но новые фичи Sunrise класть в `_Sunrise`.

## Архитектурные инварианты C#

- **Интеракции (`OnEvent -> TryDo -> CanDo -> Do`)**: обработчик `On[EventName]` только проверяет `args.Handled` и вызывает `public bool TryDo(Entity<TComp?> ent, ...)`. `TryDo` обязан сначала вызвать `public bool CanDo(Entity<TComp?> ent, ..., bool quiet = false)`; в `CanDo` **запрещено** менять состояние компонентов (разрешены только попапы при `!quiet`). Подробнее — в `ss14-ecs-systems`.
- **`EntityQuery<T>` в `EntitySystem`**: в `EntitySystem`, `GameRuleSystem` и их `partial`-файлах объявлять только через `[Dependency] private EntityQuery<T> _fooQuery = default!;` (проверяя все `partial`-файлы на дубли), не через `GetEntityQuery<T>()` в `Initialize()`.
- **`EntityQuery<T>` вне `EntitySystem`**: регистрация `EntityQuery<>` есть только в `SystemDependencyCollection`. **Запрещено** добавлять `[Dependency] EntityQuery<T>` в `IConsoleCommand`, HTN-операторы, UI-контролы, оверлеи и хелперы — используй `IEntityManager` (`TryGetComponent`, `HasComponent`, `GetEntityQuery<T>()`) или передавай query через конструктор.
- Не смешивать поле `EntityQuery<T>` с перечисляющими `EntityQueryEnumerator<T...>()`, `EntityQuery<T...>()` и `AllEntityQuery<T...>()`.

## Язык комментариев и словарь

- Пояснения в inline-комментариях, у `using` и в reason-фразах у `Sunrise-Edit` / `Sunrise edit|added start` писать по-русски (`/// <summary>` — по-английски согласно `ss14-documentation-writing`).
- Английские идентификаторы, имена типов, API, loc-key и маркеры не переводить.
- Для `Sponsor*` в описаниях использовать термин **«спонсор»** (не `donor` / `donator`).

## RIDER MCP

При доступном Rider MCP всегда используй его вместо shell-аналогов:
1. Навигация: `search_symbol`, `search_text`, `search_regex`, `search_file`, `list_directory_tree`.
2. Анализ: `read_file`, `get_symbol_info`, `get_file_problems` (исправляй ошибки/предупреждения Rider в затронутых файлах, кроме ложных срабатываний на задуманный public API).
3. Правки: `replace_text_in_file`, `rename_refactoring`, `reformat_file`.
4. Запрещено: `execute_terminal_command`, `execute_run_configuration`, `get_run_configurations`, `get_project_modules`, `get_project_dependencies`.

## ТЕСТИРОВАНИЕ

В конце работы выполни проверку изменённой области (не более 1–2 тестов одновременно; всегда останавливай запущенные процессы после проверки):
1. **YAML / FTL**: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build` (сборка при необходимости: `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Release --no-restore /m`).
2. **C# проект**: `dotnet build <path/to/project.csproj> --configuration Debug` (CI-проверка: `--configuration Release --no-restore /m`).
3. **Клиент**: `dotnet run --project Content.Client/Content.Client.csproj` (или `--configuration Tools`) для проверки runtime и IL verification, затем остановить процесс.
4. **Тесты**:
   - Один тест: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --filter "FullyQualifiedName~GravityGridTest"`
   - Проект: `dotnet test Content.Tests/Content.Tests.csproj --configuration DebugOpt --no-build` или `Content.IntegrationTests/Content.IntegrationTests.csproj`
   - Всё решение: `dotnet test SpaceStation14.slnx --configuration DebugOpt --no-build`
5. **Publish**: `dotnet publish Content.Packaging/Content.Packaging.csproj --configuration Release -r win-x64` (или `-p:PublishProfile=<ProfileName>`).
