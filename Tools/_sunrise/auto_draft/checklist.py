import re


MARKER = "<!-- auto-draft-checklist:v1 -->"


def plain(text):
    text = str(text).replace("@", "＠").replace("#", "＃")
    text = re.sub(r"[\r\n]+", " ", text)
    return re.sub(r"([\\`*_{}\[\]<>()!|])", r"\\\1", text)


def build_checklist(*, owner, repo, number, feedback, readiness, manual_draft=False,
                    manual_override=False, **_):
    pr_url = f"https://github.com/{owner}/{repo}/pull/{number}"

    def checkbox(done, text):
        return f"- [{'x' if done else ' '}] {text}"

    lines = [
        MARKER,
        "### Готовим изменения к ревью",
        "",
        "Привет! Здесь видно, что осталось сделать перед проверкой человеком. Пролистай страницу ПР вниз до блока проверок: там видны тесты и их результаты. Галочки в этом списке обновляются автоматически.",
        "",
        checkbox(
            all(item["done"] for item in feedback),
            f"Разобраться с замечаниями. Исправь код и закрой решённые обсуждения во вкладке [Files changed — изменённые файлы]({pr_url}/files). Если не согласен, обсуди это с ревьювером.",
        ),
        *(f"  {checkbox(item['done'], plain(item['text']))}" for item in feedback),
    ]

    rabbit_unresolved = readiness.get("code_rabbit_conversations_unresolved", 0)
    rabbit_total = readiness.get("code_rabbit_conversations_total", 0)
    rabbit_blocking_without_threads = readiness.get("code_rabbit_blocking_without_threads", 0)
    rabbit_review_ready = readiness.get("code_rabbit_review_ready", readiness.get("code_rabbit_ready", False))
    if readiness.get("code_rabbit_absent") and not rabbit_unresolved:
        lines.append(checkbox(
            True,
            f"~~Дождаться CodeRabbit~~ — бот не появился за {readiness['code_rabbit_wait_minutes']} минут, поэтому ожидание пропущено.",
        ))
    else:
        if rabbit_blocking_without_threads:
            rabbit_text = "Получить новое решение CodeRabbit: он запросил исправления, но не оставил обсуждений, которые можно закрыть."
        elif rabbit_unresolved:
            rabbit_text = (
                f"Закрыть все обсуждения CodeRabbit во вкладке [Files changed — изменённые файлы]({pr_url}/files). "
                f"Осталось незакрытых: {rabbit_unresolved}."
                if rabbit_review_ready else
                f"Дождаться CodeRabbit и закрыть все его обсуждения во вкладке "
                f"[Files changed — изменённые файлы]({pr_url}/files). Осталось незакрытых: {rabbit_unresolved}."
            )
        else:
            rabbit_text = (
                "Проверка CodeRabbit: достигнут лимит запросов, поэтому сейчас разрешено продолжить без нового ревью."
                if readiness.get("rate_limited") else
                "Дождаться CodeRabbit: он должен проверить последнюю версию кода. Если бот не появится за 10 минут после создания ПР, этот пункт будет пропущен."
            )
        lines.extend([
            checkbox(readiness.get("code_rabbit_ready", False), rabbit_text),
            "  CodeRabbit — искусственный интеллект: он может ошибаться и предлагать бессмысленные исправления. Сам проверь, действительно ли найден баг. Исправляй настоящие ошибки, а с неверным замечанием объясни своё несогласие в обсуждении.",
        ])
        if rabbit_total > 20 and rabbit_unresolved:
            lines.extend([
                "",
                "> [!TIP]",
                "> У CodeRabbit много обсуждений. Если галочка не закрывается, пролистай все изменённые файлы: возможно, где-то осталось незамеченное незакрытое обсуждение.",
            ])

    lines.append(checkbox(
        readiness.get("checks_ready", False),
        f"Пройти обязательные проверки внизу страницы ПР. Нажми на нужную проверку, чтобы посмотреть результат. Также доступна вкладка [Checks — проверки]({pr_url}/checks). Жёлтая проверка ещё выполняется; красная завершилась с ошибкой.",
    ))
    check_items = readiness.get("check_items", [])
    if check_items:
        lines.extend([
            "",
            "<details>",
            "<summary>Показать обязательные проверки</summary>",
            "",
            *(checkbox(item["done"], plain(item["name"])) for item in check_items),
            "",
            "</details>",
        ])

    lines.extend([
        "",
        "<details>",
        "<summary>Как найти список ошибок тестов</summary>",
        "",
        "1. Пролистай ПР вниз до блока проверок и нажми на упавший тест. Можно также открыть его во вкладке Checks.",
        "2. На странице задания нажми Summary — сводка запуска.",
        "3. В сводке раскрой нужный шард — группу тестов, например Integration Tests (shard 0). Там будет список ошибок.",
        "4. Исправь причину и отправь изменения в этот ПР. Если сводка не содержит ошибок, открой журнал упавшего шага задания.",
        "",
        "</details>",
    ])
    if readiness.get("error"):
        lines.extend(["", "Не удалось получить часть данных GitHub. Бот попробует ещё раз; пока неподтверждённые пункты не отмечены."])
    if manual_draft:
        lines.extend(["", checkbox(False, "Подтвердить готовность своего черновика: когда закончишь работу, нажми Ready for review — готово к ревью. Бот сохраняет черновики, созданные вручную.")])
    lines.extend(["", (
        "Сейчас действует ручной аварийный переход в готовое состояние. Старые замечания не вернут этот ПР в черновик; новое требование исправлений снова включит автоматику."
        if manual_override else
        "Остальные пункты помогут подготовить изменения перед ручным открытием."
        if manual_draft else
        "Когда все пункты выполнены, бот сам переведёт ПР из черновика в готовое состояние. Обновление иногда занимает несколько минут."
    )])
    return "\n".join(lines)


def sync_checklist(*, github, owner, repo, number, app_slug, comments=None, **state):
    if not app_slug:
        raise RuntimeError("Не задано имя приложения автодрафта.")
    body = build_checklist(owner=owner, repo=repo, number=number, **state)
    comments = comments if comments is not None else github.paginate(
        f"/repos/{owner}/{repo}/issues/{number}/comments"
    )
    owned = sorted((comment for comment in comments
                    if (comment.get("user") or {}).get("type") == "Bot"
                    and (comment.get("user") or {}).get("login") == f"{app_slug}[bot]"
                    and MARKER in (comment.get("body") or "")),
                   key=lambda comment: comment["id"])
    if not owned:
        github.request("POST", f"/repos/{owner}/{repo}/issues/{number}/comments", {"body": body})
    elif owned[0].get("body") != body:
        github.request("PATCH", f"/repos/{owner}/{repo}/issues/comments/{owned[0]['id']}", {"body": body})
    for duplicate in owned[1:]:
        github.request("DELETE", f"/repos/{owner}/{repo}/issues/comments/{duplicate['id']}")

    return body
