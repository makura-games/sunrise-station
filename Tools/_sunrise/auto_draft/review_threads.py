import json
import os
import re
import tomllib
from pathlib import Path
from urllib.parse import quote

from checklist import plain, sync_checklist
from github_api import GitHub
from readiness import load_readiness, timestamp
from report import build_report, publish_report


CONFIG_PATH = Path(__file__).with_name("config.toml")
CONNECTIONS = ("labels", "latestOpinionatedReviews", "reviewThreads")
PULL_REQUEST_QUERY = """
query($owner: String!, $repo: String!, $number: Int!,
      $labelsCursor: String, $latestOpinionatedReviewsCursor: String, $reviewThreadsCursor: String,
      $loadlabels: Boolean!, $loadlatestOpinionatedReviews: Boolean!, $loadreviewThreads: Boolean!) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $number) {
      id
      number
      state
      isDraft
      headRefOid
      baseRefName
      labels(first: 100, after: $labelsCursor) @include(if: $loadlabels) {
        pageInfo { hasNextPage endCursor }
        nodes { name }
      }
      latestOpinionatedReviews(first: 100, after: $latestOpinionatedReviewsCursor)
        @include(if: $loadlatestOpinionatedReviews) {
        pageInfo { hasNextPage endCursor }
        nodes { id state submittedAt authorCanPushToRepository author { login } }
      }
      reviewThreads(first: 100, after: $reviewThreadsCursor) @include(if: $loadreviewThreads) {
        pageInfo { hasNextPage endCursor }
        nodes {
          isResolved
          comments(first: 1) {
            nodes { pullRequestReview { id state author { login } } }
          }
        }
      }
      timelineItems(last: 1, itemTypes: [READY_FOR_REVIEW_EVENT]) {
        nodes { ... on ReadyForReviewEvent { createdAt actor { login } } }
      }
    }
  }
}
"""


class Core:
    def info(self, message):
        print(message)

    def warning(self, message):
        print(f"::warning::{message}")

    def error(self, message):
        print(f"::error::{message}")

    def start_group(self, message):
        print(f"::group::{message}")

    def end_group(self):
        print("::endgroup::")

    def summary(self, text):
        path = os.getenv("GITHUB_STEP_SUMMARY")
        if path:
            with open(path, "a", encoding="utf-8") as summary_file:
                summary_file.write(text)


def load_config(path=CONFIG_PATH):
    with open(path, "rb") as config_file:
        return tomllib.load(config_file)


# AUTO_DRAFT_POLICY_START
def decide_draft_state(*, is_draft, has_marker, latest_blocking_at, latest_ready_at,
                       all_blocking_threads_resolved, checks_ready, code_rabbit_ready,
                       keep_ready_during_rerun=False):
    has_blocking_review = latest_blocking_at is not None
    should_be_ready = ((not has_blocking_review or all_blocking_threads_resolved)
                       and checks_ready and code_rabbit_ready)

    if is_draft:
        return "ready" if has_marker and should_be_ready else "keep"

    manual_override = latest_ready_at is not None and (
        not has_blocking_review or latest_ready_at >= latest_blocking_at
    )
    waiting_for_rerun = ((not has_blocking_review or all_blocking_threads_resolved)
                         and code_rabbit_ready and keep_ready_during_rerun)
    if not should_be_ready and not waiting_for_rerun and not manual_override:
        return "draft"

    return "cleanup" if has_marker else "keep"
# AUTO_DRAFT_POLICY_END


def latest_timestamp(items):
    return max((timestamp(item["submittedAt"]) for item in items), default=None)


def is_coderabbit_review(review):
    login = ((review or {}).get("author") or {}).get("login", "")
    return login.removesuffix("[bot]") == "coderabbitai"


def coderabbit_conversation_state(threads, blocking_reviews):
    rabbit_threads = []
    threads_by_review = {}
    for thread in threads:
        comments = thread.get("comments", {}).get("nodes", [])
        review = comments[0].get("pullRequestReview") if comments else None
        if not is_coderabbit_review(review):
            continue
        rabbit_threads.append(thread)
        threads_by_review.setdefault(review["id"], []).append(thread)

    rabbit_blocking_reviews = [review for review in blocking_reviews if is_coderabbit_review(review)]
    unresolved = sum(not thread["isResolved"] for thread in rabbit_threads)
    blocking_without_threads = sum(not threads_by_review.get(review["id"])
                                   for review in rabbit_blocking_reviews)
    blocking_resolved = all(
        threads_by_review.get(review["id"])
        and all(thread["isResolved"] for thread in threads_by_review[review["id"]])
        for review in rabbit_blocking_reviews
    )
    return {
        "total": len(rabbit_threads),
        "unresolved": unresolved,
        "blocking_without_threads": blocking_without_threads,
        "resolved": unresolved == 0 and blocking_resolved,
    }


class AutoDraft:
    def __init__(self, *, github, context, core, config=None, read_github=None):
        self.github = github
        self.read_github = read_github or github
        self.context = context
        self.core = core
        self.owner = context["owner"]
        self.repo = context["repo"]
        self.config = config or load_config()
        self.label = self.config.get("label")
        self._validate_label()
        self.marker_label = self.label["name"]
        self.marker_names = list(dict.fromkeys([self.marker_label, *self.label["previous_names"]]))
        self.app_slug = os.getenv("AUTO_DRAFT_APP_SLUG")
        self.report_app_slug = self.app_slug if self.read_github is self.github else "github-actions"
        self.rules_cache = {}
        self.current_pull_request = None
        self.current_report_check = None
        self.current_report_check_loaded = False

    def _validate_label(self):
        def valid_name(name):
            return isinstance(name, str) and 0 < len(name.strip()) <= 50 and not re.search(r"[\r\n]", name)

        label = self.label
        if (not label or not valid_name(label.get("name"))
                or not re.fullmatch(r"[a-f0-9]{6}", label.get("color", ""), re.I)
                or not isinstance(label.get("description"), str) or len(label["description"]) > 100
                or not isinstance(label.get("previous_names"), list)
                or not all(valid_name(name) for name in label["previous_names"])):
            raise RuntimeError("Некорректная метка в auto_draft/config.toml: проверь name, шестизначный color, description и previous_names.")

    def ensure_label(self):
        existing = None
        for name in self.marker_names:
            try:
                existing = self.github.request(
                    "GET", f"/repos/{self.owner}/{self.repo}/labels/{quote(name, safe='')}"
                )
                break
            except Exception as error:
                if getattr(error, "status", None) != 404:
                    raise
        body = {
            "name": self.marker_label,
            "color": self.label["color"],
            "description": self.label["description"],
        }
        if not existing:
            self.github.request("POST", f"/repos/{self.owner}/{self.repo}/labels", body)
        elif (existing.get("name") != self.marker_label
              or existing.get("color", "").lower() != self.label["color"].lower()
              or existing.get("description") != self.label["description"]):
            self.github.request(
                "PATCH",
                f"/repos/{self.owner}/{self.repo}/labels/{quote(existing['name'], safe='')}",
                {"new_name": self.marker_label, "color": self.label["color"],
                 "description": self.label["description"]},
            )

    def load_pull_request(self, number):
        pull_request = None
        while True:
            variables = {"owner": self.owner, "repo": self.repo, "number": number}
            for name in CONNECTIONS:
                page = (pull_request or {}).get(name, {}).get("pageInfo")
                variables[f"{name}Cursor"] = page.get("endCursor") if page else None
                variables[f"load{name}"] = not page or page.get("hasNextPage", False)
            result = self.github.graphql(PULL_REQUEST_QUERY, variables)
            next_page = result["repository"]["pullRequest"]
            if not next_page:
                return None
            if pull_request is None:
                pull_request = next_page
            else:
                for name in CONNECTIONS:
                    if name not in next_page:
                        continue
                    pull_request[name]["nodes"].extend(next_page[name]["nodes"])
                    pull_request[name]["pageInfo"] = next_page[name]["pageInfo"]
            if not any(pull_request[name]["pageInfo"].get("hasNextPage") for name in CONNECTIONS):
                return pull_request

    def add_marker(self, number):
        self.github.request(
            "POST", f"/repos/{self.owner}/{self.repo}/issues/{number}/labels",
            {"labels": [self.marker_label]},
        )

    def remove_marker(self, number, names=None):
        for name in names or self.marker_names:
            try:
                self.github.request(
                    "DELETE",
                    f"/repos/{self.owner}/{self.repo}/issues/{number}/labels/{quote(name, safe='')}",
                )
            except Exception as error:
                if getattr(error, "status", None) != 404:
                    raise

    def convert_to_draft(self, pull_request_id):
        self.github.graphql("""
          mutation($id: ID!) {
            convertPullRequestToDraft(input: { pullRequestId: $id }) { pullRequest { id } }
          }
        """, {"id": pull_request_id})

    def mark_ready_for_review(self, pull_request_id):
        self.github.graphql("""
          mutation($id: ID!) {
            markPullRequestReadyForReview(input: { pullRequestId: $id }) { pullRequest { id } }
          }
        """, {"id": pull_request_id})

    def sync_pull_request(self, number):
        self.core.info("Этап 1/4: читаю состояние ПР, решения ревьюверов и обсуждения.")
        pull_request = self.load_pull_request(number)
        self.current_pull_request = pull_request
        if not pull_request or pull_request["state"] != "OPEN":
            self.core.info(f"#{number}: ПР закрыт или не найден, пропускаю.")
            return build_report(number=number, skipped="ПР закрыт или не найден: синхронизация не нужна.")

        reviews = [review for review in pull_request["latestOpinionatedReviews"]["nodes"]
                   if review["authorCanPushToRepository"]]
        blocking_reviews = [review for review in reviews if review["state"] == "CHANGES_REQUESTED"]
        approvals = [review for review in reviews if review["state"] == "APPROVED"]
        rabbit_conversations = coderabbit_conversation_state(
            pull_request["reviewThreads"]["nodes"], blocking_reviews
        )
        blocking_review_ids = {review["id"] for review in blocking_reviews}
        blocking_authors = {(review.get("author") or {}).get("login") for review in blocking_reviews
                            if (review.get("author") or {}).get("login")}
        threads_by_review = {}
        unresolved_by_author = set()
        has_unresolved_blocking_threads = False

        for thread in pull_request["reviewThreads"]["nodes"]:
            comments = thread.get("comments", {}).get("nodes", [])
            review = comments[0].get("pullRequestReview") if comments else None
            if (not review or not (review["id"] in blocking_review_ids
                    or review.get("state") == "CHANGES_REQUESTED"
                    and (review.get("author") or {}).get("login") in blocking_authors)):
                continue
            has_unresolved_blocking_threads = has_unresolved_blocking_threads or not thread["isResolved"]
            author = (review.get("author") or {}).get("login")
            if not thread["isResolved"] and author:
                unresolved_by_author.add(author)
            threads_by_review.setdefault(review["id"], []).append(thread)

        all_blocking_threads_resolved = bool(blocking_reviews) and not has_unresolved_blocking_threads and all(
            threads_by_review.get(review["id"])
            and all(thread["isResolved"] for thread in threads_by_review[review["id"]])
            for review in blocking_reviews
        )
        latest_blocking_at = latest_timestamp(blocking_reviews)
        ready_nodes = pull_request.get("timelineItems", {}).get("nodes", [])
        latest_ready_event = ready_nodes[0] if ready_nodes else None
        ready_login = ((latest_ready_event or {}).get("actor") or {}).get("login", "").removesuffix("[bot]")
        ready_by_app = bool(self.app_slug and ready_login == self.app_slug)
        latest_ready_at = (timestamp(latest_ready_event["createdAt"])
                           if latest_ready_event and not ready_by_app else None)
        has_marker = any(label["name"] in self.marker_names for label in pull_request["labels"]["nodes"])
        manual_override = (not pull_request["isDraft"] and latest_ready_at is not None
                           and (latest_blocking_at is None or latest_ready_at >= latest_blocking_at))
        readiness = {
            "checks_ready": False,
            "code_rabbit_ready": False,
            "pending_checks": [],
            "check_items": [],
        }
        readiness_error = None
        self.core.info("Этап 2/4: проверяю обязательные тесты и ответ CodeRabbit.")
        try:
            readiness = load_readiness(
                github=self.read_github,
                comments_github=self.github,
                owner=self.owner,
                repo=self.repo,
                pull_request=pull_request,
                rules_cache=self.rules_cache,
                report_app_slug=self.report_app_slug,
            )
            self.current_report_check = readiness["report_check"]
            self.current_report_check_loaded = True
        except Exception as error:
            readiness_error = error
            readiness["error"] = True
            self.core.warning(f"#{number}: не удалось получить готовность проверок: {error}")

        readiness["code_rabbit_review_ready"] = readiness["code_rabbit_ready"]
        readiness["code_rabbit_conversations_total"] = rabbit_conversations["total"]
        readiness["code_rabbit_conversations_unresolved"] = rabbit_conversations["unresolved"]
        readiness["code_rabbit_blocking_without_threads"] = rabbit_conversations["blocking_without_threads"]
        readiness["code_rabbit_conversations_resolved"] = rabbit_conversations["resolved"]
        readiness["code_rabbit_ready"] &= rabbit_conversations["resolved"]

        action = decide_draft_state(
            is_draft=pull_request["isDraft"],
            has_marker=has_marker,
            latest_blocking_at=latest_blocking_at,
            latest_ready_at=latest_ready_at,
            all_blocking_threads_resolved=all_blocking_threads_resolved,
            checks_ready=readiness["checks_ready"],
            code_rabbit_ready=readiness["code_rabbit_ready"],
            keep_ready_during_rerun=readiness.get("keep_ready_during_rerun", False),
        )
        self.core.info(
            f"#{number}: action={action}, draft={pull_request['isDraft']}, "
            f"blocking={len(blocking_reviews)}, approvals={len(approvals)}, "
            f"threadsResolved={all_blocking_threads_resolved}, checksReady={readiness['checks_ready']}, "
            f"codeRabbitReady={readiness['code_rabbit_ready']}, "
            f"codeRabbitThreads={rabbit_conversations['total']}/{rabbit_conversations['unresolved']}, "
            f"rateLimited={readiness.get('rate_limited', False)}, "
            f"pendingChecks={', '.join(plain(name) for name in readiness['pending_checks'])}."
        )

        feedback = []
        for review in blocking_reviews:
            if is_coderabbit_review(review):
                continue
            threads = threads_by_review.get(review["id"], [])
            author = (review.get("author") or {}).get("login") or "ревьювера"
            suffix = "" if threads else ": требуется новое решение ревьювера, обсуждений у этого требования нет"
            feedback.append({
                "text": f"Замечания {author}{suffix}",
                "done": bool(threads and all(thread["isResolved"] for thread in threads)
                             and author not in unresolved_by_author),
            })
        manual_draft = pull_request["isDraft"] and not has_marker
        report_state = {
            "number": number,
            "feedback": feedback,
            "readiness": readiness,
            "action": action,
            "manual_draft": manual_draft,
            "manual_override": manual_override,
        }
        self.core.info("Этап 3/4: обновляю список задач в комментарии ПР.")
        sync_checklist(
            github=self.github,
            owner=self.owner,
            repo=self.repo,
            number=number,
            app_slug=self.app_slug,
            feedback=feedback,
            readiness=readiness,
            manual_draft=manual_draft,
            manual_override=manual_override,
            comments=readiness.get("comments"),
        )
        if readiness_error:
            raise readiness_error

        if action in {"draft", "ready"}:
            current = self.github.request("GET", f"/repos/{self.owner}/{self.repo}/pulls/{number}")
            if (current["head"]["sha"] != pull_request["headRefOid"]
                    or current["base"]["ref"] != pull_request["baseRefName"]
                    or current["draft"] != pull_request["isDraft"] or current["state"] != "open"):
                self.core.info(f"#{number}: состояние ПР изменилось во время проверки, откладываю синхронизацию.")
                return build_report(
                    **report_state,
                    skipped="ПР изменился во время чтения. Устаревшее решение не применяется; следующий запуск перечитает данные.",
                )

        self.core.info(f"Этап 4/4: {build_report(**report_state)['title']}.")
        old_markers = [label["name"] for label in pull_request["labels"]["nodes"]
                       if label["name"] != self.marker_label and label["name"] in self.marker_names]
        if action == "keep" and pull_request["isDraft"] and old_markers:
            if not any(label["name"] == self.marker_label for label in pull_request["labels"]["nodes"]):
                self.add_marker(number)
            self.remove_marker(number, old_markers)
        if action == "draft":
            if not has_marker:
                self.add_marker(number)
            try:
                self.convert_to_draft(pull_request["id"])
            except Exception:
                if not has_marker:
                    self.remove_marker(number)
                raise
            return build_report(**report_state)
        if action == "ready":
            self.mark_ready_for_review(pull_request["id"])
            self.remove_marker(number)
            return build_report(**report_state)
        if action == "cleanup":
            self.remove_marker(number)
        return build_report(**report_state)

    def open_pull_request_numbers(self):
        pulls = self.github.paginate(
            f"/repos/{self.owner}/{self.repo}/pulls", params={"state": "open"}
        )
        return [pull["number"] for pull in pulls]

    def associated_pull_request_numbers(self, sha):
        if not sha:
            return []
        try:
            pulls = self.github.paginate(
                f"/repos/{self.owner}/{self.repo}/commits/{sha}/pulls"
            )
            return list(dict.fromkeys(pull["number"] for pull in pulls if pull["state"] == "open"))
        except Exception as error:
            if getattr(error, "status", None) != 404:
                raise
            self.core.info("Коммит события уже недоступен; связанных ПР не найдено.")
            return []

    def target_pull_request_numbers(self):
        event_name = self.context["event_name"]
        payload = self.context["payload"]
        if event_name == "issue_comment":
            issue = payload["issue"]
            if not issue.get("pull_request"):
                return []
            sender = payload["sender"]
            comment = payload.get("comment") or {}
            rabbit = sender["type"] == "Bot" and sender["login"] == "coderabbitai[bot]"
            comment_user = comment.get("user") or {}
            checklist_edited = (payload.get("action") in {"edited", "deleted"}
                                and comment_user.get("type") == "Bot"
                                and comment_user.get("login") == f"{self.app_slug}[bot]"
                                and sender["login"] != comment_user.get("login"))
            return [issue["number"]] if rabbit or checklist_edited else []
        if event_name == "status":
            return self.associated_pull_request_numbers(payload.get("sha"))
        if event_name == "workflow_run":
            workflow_run = payload["workflow_run"]
            if (workflow_run["name"] == "PR: Automatic Draft Management - Review Events"
                    and workflow_run.get("conclusion") != "success"):
                self.core.info("Сигнальный workflow завершился неуспешно, синхронизация не требуется.")
                return []
            numbers = [pull["number"] for pull in workflow_run.get("pull_requests", [])
                       if isinstance(pull.get("number"), int) and pull["number"] > 0]
            if not numbers and workflow_run.get("head_sha"):
                numbers.extend(self.associated_pull_request_numbers(workflow_run["head_sha"]))
            if not numbers:
                self.core.info("workflow_run не содержит связанного ПР; проверяю открытые ПР.")
                return self.open_pull_request_numbers()
            return list(dict.fromkeys(numbers))
        if event_name == "pull_request_target":
            return [payload["pull_request"]["number"]]
        if event_name == "workflow_dispatch":
            requested = (payload.get("inputs") or {}).get("pr-number")
            if requested in {None, ""}:
                return self.open_pull_request_numbers()
            try:
                number = int(requested)
            except (TypeError, ValueError):
                number = 0
            if number <= 0 or number > 2**53 - 1:
                raise RuntimeError(f"Некорректный номер ПР: {requested}")
            return [number]
        return self.open_pull_request_numbers()

    def run(self):
        numbers = self.target_pull_request_numbers()
        self.core.info(f"Автодрафт запущен: событие {self.context['event_name']}; ПР для проверки: {len(numbers)}.")
        if not numbers:
            self.core.info("Ничего не изменено: событие не требует пересчёта открытых ПР.")
            return
        self.ensure_label()
        failures = []
        for number in numbers:
            self.current_pull_request = None
            self.current_report_check = None
            self.current_report_check_loaded = False
            self.core.start_group(f"ПР {number}: проверка готовности")
            try:
                report = self.sync_pull_request(number)
                publish_report(
                    github=self.read_github,
                    core=self.core,
                    owner=self.owner,
                    repo=self.repo,
                    number=number,
                    head=self.current_pull_request.get("headRefOid")
                    if self.current_pull_request and self.current_pull_request.get("state") == "OPEN" else None,
                    report=report,
                    run_id=self.context.get("run_id"),
                    existing=self.current_report_check,
                    existing_loaded=self.current_report_check_loaded,
                    check_app_slug=self.report_app_slug,
                )
            except Exception as error:
                failures.append(f"#{number}: {error}")
                self.core.error(f"#{number}: {error}")
                headers = getattr(error, "headers", {})
                status = getattr(error, "status", None)
                rate_limited = (status == 429 or headers.get("x-ratelimit-remaining") == "0"
                                or status == 403 and re.search(r"rate.?limit|secondary.*limit", str(error), re.I))
                try:
                    publish_report(
                        github=self.read_github,
                        core=self.core,
                        owner=self.owner,
                        repo=self.repo,
                        number=number,
                        head=None if rate_limited else (
                            self.current_pull_request.get("headRefOid") if self.current_pull_request else None
                        ),
                        report=build_report(number=number, error=error),
                        run_id=self.context.get("run_id"),
                        existing=self.current_report_check,
                        existing_loaded=self.current_report_check_loaded,
                        check_app_slug=self.report_app_slug,
                    )
                except Exception as report_error:
                    self.core.error(f"Не удалось опубликовать результат ПР {number}: {report_error}")
                if rate_limited:
                    self.core.warning("GitHub ограничил запросы. Обход остановлен без повторных запросов; оставшиеся ПР не получили подтверждение готовности. Повтори запуск после восстановления лимита.")
                    break
            finally:
                self.core.end_group()
        if failures:
            raise RuntimeError(f"Не удалось синхронизировать {len(failures)} ПР:\n" + "\n".join(failures))
        self.core.info(f"✅ Синхронизация завершена успешно: обработано {len(numbers)} ПР. Причины решений записаны выше и в сводке запуска.")


def main():
    repository = os.environ["GITHUB_REPOSITORY"].split("/", 1)
    with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as event_file:
        payload = json.load(event_file)
    context = {
        "owner": repository[0],
        "repo": repository[1],
        "event_name": os.environ["GITHUB_EVENT_NAME"],
        "payload": payload,
        "run_id": os.getenv("GITHUB_RUN_ID"),
    }
    core = Core()
    try:
        AutoDraft(
            github=GitHub(os.environ["GH_TOKEN"], os.getenv("GITHUB_API_URL", "https://api.github.com")),
            context=context,
            core=core,
        ).run()
    except Exception as error:
        core.error(f"Автодрафт не завершился: {error}")
        core.summary(f"# ❌ Автодрафт не завершился\n\n```\n{error}\n```\n")
        raise


if __name__ == "__main__":
    main()
