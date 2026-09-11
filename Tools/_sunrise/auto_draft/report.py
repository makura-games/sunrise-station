import hashlib

from checklist import plain


RESULTS = {
    "EXPECTED": "ещё не запущена",
    "QUEUED": "в очереди",
    "PENDING": "ожидается",
    "IN_PROGRESS": "выполняется",
    "SUCCESS": "успешно",
    "FAILURE": "ошибка",
    "ERROR": "ошибка",
    "CANCELLED": "отменена",
    "TIMED_OUT": "истекло время",
    "SKIPPED": "пропущена по условию",
    "NEUTRAL": "нейтральный результат",
    "ACTION_REQUIRED": "нужно действие человека",
    "STARTUP_FAILURE": "не удалось запустить проверку",
}
FAILED_RESULTS = {"FAILURE", "ERROR", "CANCELLED", "TIMED_OUT", "ACTION_REQUIRED", "STARTUP_FAILURE"}


def build_report(*, number, feedback=None, readiness=None, manual_draft=False,
                 manual_override=False, action=None, error=None, skipped=None):
    feedback = feedback or []
    readiness = readiness or {}
    unresolved = [item for item in feedback if not item["done"]]

    def failed_item(item):
        return not item["done"] and item.get("result") in FAILED_RESULTS

    failed = any(failed_item(item) for item in readiness.get("check_items", []))
    reason = "всё готово"
    if error:
        reason = "ошибка синхронизации"
    elif skipped:
        reason = "повторная проверка позже"
    elif manual_override:
        reason = "ручной режим"
    elif manual_draft:
        reason = "ручной черновик"
    elif unresolved:
        reason = "нужны исправления"
    elif failed:
        reason = "ошибки проверок"
    elif not readiness.get("checks_ready"):
        reason = "повторный запуск тестов" if readiness.get("keep_ready_during_rerun") else "ждём проверки"
    elif readiness.get("code_rabbit_blocking_without_threads", 0):
        reason = "нужно новое решение CodeRabbit"
    elif readiness.get("code_rabbit_conversations_unresolved", 0):
        reason = "нужно закрыть обсуждения CodeRabbit"
    elif not readiness.get("code_rabbit_ready"):
        reason = "ждём CodeRabbit"

    conclusion = "failure" if error else "success" if reason == "всё готово" else "neutral"
    title = f"Автодрафт: {reason}"
    lines = [f"## {'❌' if error else '✅' if conclusion == 'success' else 'ℹ️'} ПР {number}: {reason}", ""]
    if error:
        lines.extend([
            f"Не удалось завершить синхронизацию: {plain(error)}",
            "",
            "Проверь сообщение об ошибке и последний этап в журнале. При отказе в доступе проверь права приложения и подтверждение его установки; при недоступности GitHub повтори запуск. Успешная готовность не подтверждена.",
        ])
    elif skipped:
        lines.append(plain(skipped))
    else:
        lines.append(f"- {'⏳ Остались требования исправлений' if unresolved else '✅ Открытых требований исправлений нет'}.")
        lines.extend(f"  - {plain(item['text'])}" for item in unresolved)
        lines.append(f"- {'✅ Обязательные проверки пройдены' if readiness.get('checks_ready') else '⏳ Обязательные проверки не завершены успешно'}.")
        for item in readiness.get("check_items", []):
            icon = "✅" if item["done"] else "❌" if failed_item(item) else "⏳"
            result = RESULTS.get(item.get("result"), item.get("result") or ("успешно" if item["done"] else "ожидается"))
            lines.append(f"  - {icon} {plain(item['name'])}: {plain(result)}.")
        rabbit_unresolved = readiness.get("code_rabbit_conversations_unresolved", 0)
        rabbit = (
            "⏳ CodeRabbit запросил исправления: нужно новое решение"
            if readiness.get("code_rabbit_blocking_without_threads", 0) else
            f"⏳ Остались незакрытые обсуждения CodeRabbit: {rabbit_unresolved}"
            if rabbit_unresolved else
            "ℹ️ CodeRabbit не появился за 10 минут: ожидание пропущено"
            if readiness.get("code_rabbit_absent") else
            "✅ CodeRabbit сообщил о лимите: исключение разрешено"
            if readiness.get("rate_limited") else
            "✅ CodeRabbit закончил ревью"
            if readiness.get("code_rabbit_ready") else
            "⏳ Ожидается CodeRabbit"
        )
        lines.extend([f"- {rabbit}.", ""])
        if manual_draft:
            lines.append("Ручной черновик сохранён: автор сам подтверждает готовность.")
        elif manual_override:
            lines.append("Сохранён ручной аварийный переход. Новое требование исправлений снова включит автоматику.")
        elif not readiness.get("checks_ready") and readiness.get("keep_ready_during_rerun") and action == "keep":
            lines.append("ПР оставлен готовым: повторяется ранее успешная проверка того же коммита. Новый провал снова заблокирует его.")
        else:
            lines.append({
                "draft": "ПР переведён в черновик.",
                "ready": "ПР открыт для ревью.",
                "cleanup": "Устаревшая служебная метка снята.",
                "keep": "Состояние ПР не изменено: оно соответствует условиям выше.",
            }.get(action))

    return {"title": title, "conclusion": conclusion, "summary": "\n".join(line for line in lines if line is not None)}


def publish_report(*, github, core, owner, repo, number, head, report, run_id=None,
                   existing=None, existing_loaded=False, check_app_slug="github-actions"):
    core.info(report["summary"])
    core.summary(report["summary"] + "\n\n")
    if not head:
        return
    prefix = f"auto-draft:{number}"
    external_id = f"{prefix}:{hashlib.sha256(report['summary'].encode()).hexdigest()}"
    if not existing_loaded:
        checks = github.paginate(
            f"/repos/{owner}/{repo}/commits/{head}/check-runs",
            key="check_runs",
            params={"filter": "all"},
        )
        matching = [check for check in checks
                    if (check.get("external_id") == prefix
                        or (check.get("external_id") or "").startswith(prefix + ":"))
                    and (check.get("app") or {}).get("slug") == check_app_slug]
        existing = max(matching, key=lambda check: check["id"], default=None)
    details_url = (f"https://github.com/{owner}/{repo}/actions/runs/{run_id}" if run_id
                   else f"https://github.com/{owner}/{repo}/pull/{number}")
    parameters = {
        "name": report["title"],
        "status": "completed",
        "conclusion": report["conclusion"],
        "external_id": external_id,
        "details_url": details_url,
        "output": {"title": report["title"], "summary": report["summary"]},
    }
    if existing:
        if (existing.get("name") != report["title"]
                or existing.get("conclusion") != report["conclusion"]
                or existing.get("external_id") != external_id):
            github.request("PATCH", f"/repos/{owner}/{repo}/check-runs/{existing['id']}", parameters)
    else:
        github.request("POST", f"/repos/{owner}/{repo}/check-runs", {**parameters, "head_sha": head})
