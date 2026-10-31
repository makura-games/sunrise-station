import re
import time
from datetime import datetime
from urllib.parse import quote


READINESS_QUERY = """
query Readiness($owner: String!, $repo: String!, $number: Int!, $cursor: String) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $number) {
      headRefOid
      baseRefName
      commits(last: 1) {
        nodes { commit { pushedDate committedDate statusCheckRollup {
          contexts(first: 100, after: $cursor) {
            pageInfo { hasNextPage endCursor }
            nodes {
              __typename
              ... on CheckRun {
                databaseId name status conclusion title externalId startedAt completedAt
                isRequired(pullRequestNumber: $number)
                checkSuite { app { databaseId slug } workflowRun { databaseId workflow { databaseId } } }
              }
              ... on StatusContext {
                context state description createdAt
                creator { login __typename }
                isRequired(pullRequestNumber: $number)
              }
            }
          }
        } } }
      }
    }
  }
}
"""

REVIEW_UNAVAILABLE = re.compile(
    r"\breview\s+(?:was\s+)?skipped\b|auto(?:matic)?\s+reviews?\s+(?:are|is)\s+"
    r"(?:disabled|not enabled)|reviews?\s+(?:are\s+)?paused\b|review\s+(?:timed\s+out|failed)\b|"
    r"(?:cannot|can't|unable to)\s+(?:perform\s+)?(?:an?\s+)?review\b",
    re.IGNORECASE,
)
MENTIONS_LIMIT = re.compile(
    r"rate[\s_-]*limit(?:ed|ing)?|(?:review|usage|request)\s+(?:limit|quota)"
    r"(?:\s+(?:has\s+been|is))?\s+(?:reached|exceeded|exhausted)|"
    r"(?:used|exhausted)\s+(?:all\s+)?(?:\w+\s+){0,4}(?:reviews|quota)|"
    r"(?:лимит|квота)\s+(?:[^\W\d_]+\s+){0,3}(?:исчерпан|превышен|достигнут)|"
    r"(?:исчерпан|превышен|достигнут)[а-я]*\s+(?:лимит|квота)",
    re.IGNORECASE,
)
CODE_RABBIT_WAIT_MINUTES = 30


def timestamp(value):
    if not value:
        return 0
    return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()


def check_key(check):
    if check["__typename"] == "CheckRun":
        suite = check.get("checkSuite") or {}
        workflow_run = suite.get("workflowRun") or {}
        workflow = workflow_run.get("workflow") or {}
        return f"run:{(suite.get('app') or {}).get('databaseId')}:{workflow.get('databaseId')}:{check.get('name')}"
    return f"status:{check.get('context')}"


def succeeded(check):
    if check["__typename"] == "StatusContext":
        return check.get("state") == "SUCCESS"
    return check.get("status") == "COMPLETED" and check.get("conclusion") in {"SUCCESS", "NEUTRAL", "SKIPPED"}


def is_rabbit(check):
    if check["__typename"] == "StatusContext":
        creator = check.get("creator") or {}
        return (check.get("context") == "CodeRabbit" and creator.get("__typename") == "Bot"
                and creator.get("login") == "coderabbitai")
    return ((check.get("checkSuite") or {}).get("app") or {}).get("slug") == "coderabbitai"


def is_unavailable_rabbit_check(check):
    if not REVIEW_UNAVAILABLE.search(check.get("description") or check.get("title") or ""):
        return False
    if check["__typename"] == "StatusContext":
        return True
    return check.get("status") == "COMPLETED"


def load_readiness(*, github, owner, repo, pull_request, rules_cache, comments_github=None,
                   report_app_slug="github-actions", now=None):
    comments_github = comments_github or github
    now = time.time() if now is None else now
    checks = []
    cursor = None
    head_updated_at = None
    while True:
        data = github.graphql(READINESS_QUERY, {
            "owner": owner,
            "repo": repo,
            "number": pull_request["number"],
            "cursor": cursor,
        })
        current = data["repository"]["pullRequest"]
        if (not current or current["headRefOid"] != pull_request["headRefOid"]
                or current["baseRefName"] != pull_request["baseRefName"]):
            raise RuntimeError("ПР изменился во время чтения проверок; требуется повторная синхронизация.")
        commits = current.get("commits", {}).get("nodes", [])
        commit = (commits[0].get("commit") or {}) if commits else {}
        head_updated_at = commit.get("pushedDate") or commit.get("committedDate")
        connection = ((commit.get("statusCheckRollup") or {}).get("contexts") if commits else None)
        checks.extend((connection or {}).get("nodes", []))
        page_info = (connection or {}).get("pageInfo", {})
        if not page_info.get("hasNextPage"):
            break
        cursor = page_info.get("endCursor")

    latest = {}
    for check in checks:
        key = check_key(check)
        previous = latest.get(key)
        order = check.get("databaseId", 0) if check["__typename"] == "CheckRun" else timestamp(check.get("createdAt"))
        previous_order = (previous.get("databaseId", 0) if previous and previous["__typename"] == "CheckRun"
                          else timestamp(previous.get("createdAt")) if previous else 0)
        if previous is None or order > previous_order:
            latest[key] = check
    current_checks = list(latest.values())
    rabbit_checks = [check for check in current_checks if is_rabbit(check)]
    def check_timestamp(check):
        return timestamp(check.get("startedAt") or check.get("createdAt") or check.get("completedAt"))

    rabbit_check = max(rabbit_checks, key=check_timestamp, default=None)
    rabbit_pending = bool(rabbit_check and (
        rabbit_check.get("state") in {"EXPECTED", "PENDING"}
        or rabbit_check["__typename"] == "CheckRun" and rabbit_check.get("status") != "COMPLETED"
    ))
    rabbit_succeeded = bool(rabbit_check and (
        rabbit_check.get("state") == "SUCCESS"
        or rabbit_check.get("status") == "COMPLETED" and rabbit_check.get("conclusion") == "SUCCESS"
    ))
    rabbit_terminal_failure = bool(rabbit_check and not rabbit_pending and not rabbit_succeeded)
    rabbit_started_at = timestamp((rabbit_check or {}).get("startedAt")
                                  or (rabbit_check or {}).get("createdAt"))
    wait_started_at = max(timestamp(head_updated_at), rabbit_started_at)

    comments = comments_github.paginate(f"/repos/{owner}/{repo}/issues/{pull_request['number']}/comments")
    rabbit_comments = [comment for comment in comments
                       if (comment.get("user") or {}).get("type") == "Bot"
                       and (comment.get("user") or {}).get("login") == "coderabbitai[bot]"]
    current_rabbit_comments = [comment for comment in rabbit_comments
                               if timestamp(comment.get("updated_at")) >= timestamp(head_updated_at)]
    def superseded(comment):
        return rabbit_pending and rabbit_started_at > timestamp(comment.get("updated_at"))

    explicitly_unavailable = is_unavailable_rabbit_check(rabbit_check) if rabbit_check else False
    explicitly_unavailable = explicitly_unavailable or any(
        REVIEW_UNAVAILABLE.search(comment.get("body") or "") and not superseded(comment)
        for comment in current_rabbit_comments
    )

    rate_limited = bool(rabbit_check and MENTIONS_LIMIT.search(
        rabbit_check.get("description") or rabbit_check.get("title") or ""
    )) or any(MENTIONS_LIMIT.search(comment.get("body") or "") and not superseded(comment)
              for comment in current_rabbit_comments)
    waiting = rabbit_check is None or rabbit_pending
    timed_out = bool(wait_started_at) and waiting and now - wait_started_at >= CODE_RABBIT_WAIT_MINUTES * 60
    code_rabbit_absent = rabbit_check is None and timed_out
    code_rabbit_unavailable = explicitly_unavailable or rabbit_terminal_failure
    code_rabbit_ready = rabbit_succeeded or code_rabbit_unavailable or rate_limited or timed_out
    code_rabbit_reviewed = rabbit_succeeded and not explicitly_unavailable and not rate_limited

    previous_success = False
    if rabbit_pending:
        if rabbit_check["__typename"] == "CheckRun":
            previous_success = any(
                check["__typename"] == "CheckRun" and is_rabbit(check)
                and check_key(check) == check_key(rabbit_check)
                and check.get("databaseId", 0) < rabbit_check.get("databaseId", 0)
                and check.get("status") == "COMPLETED" and check.get("conclusion") == "SUCCESS"
                for check in checks
            )
        else:
            statuses = github.paginate(
                f"/repos/{owner}/{repo}/commits/{pull_request['headRefOid']}/statuses"
            )
            previous_success = any(
                status.get("context") == "CodeRabbit" and status.get("state") == "success"
                and (status.get("creator") or {}).get("type") == "Bot"
                and (status.get("creator") or {}).get("login") == "coderabbitai[bot]"
                for status in statuses
            )

    branch_name = pull_request["baseRefName"]
    if branch_name not in rules_cache:
        rules_cache[branch_name] = github.paginate(
            f"/repos/{owner}/{repo}/rules/branches/{quote(branch_name, safe='')}"
        )
    classic_key = f"classic:{branch_name}"
    if classic_key not in rules_cache:
        branch = github.request("GET", f"/repos/{owner}/{repo}/branches/{quote(branch_name, safe='')}")
        protection = branch.get("protection") or {}
        required = protection.get("required_status_checks") or {}
        if protection.get("enabled") is False or required.get("enforcement_level") == "off":
            classic = []
        elif required.get("checks"):
            classic = required["checks"]
        else:
            classic = [{"context": context} for context in required.get("contexts", [])]
        rules_cache[classic_key] = classic

    requirements = [{"context": check["context"], "integration_id": check.get("app_id")}
                    for check in rules_cache[classic_key]]
    workflows = []
    for rule in rules_cache[branch_name]:
        if rule["type"] == "required_status_checks":
            requirements.extend(rule["parameters"]["required_status_checks"])
        if rule["type"] == "workflows":
            workflows.extend(rule["parameters"]["workflows"])

    required_checks = [check for check in current_checks if check.get("isRequired")]
    missing_checks = [requirement for requirement in requirements if not any(
        (check.get("name") or check.get("context")) == requirement["context"]
        and (requirement.get("integration_id") in {None, -1}
             or check["__typename"] == "StatusContext"
             or ((check.get("checkSuite") or {}).get("app") or {}).get("databaseId") == requirement["integration_id"])
        for check in required_checks
    )]
    check_items = [
        *({"name": check["context"], "done": False, "result": "EXPECTED"} for check in missing_checks),
        *({
            "name": check.get("name") or check.get("context"),
            "done": code_rabbit_ready if is_rabbit(check) else succeeded(check),
            "result": check.get("conclusion") or check.get("status") or check.get("state"),
        } for check in required_checks),
    ]

    runs = None

    def load_runs():
        nonlocal runs
        if runs is None:
            runs = github.paginate(
                f"/repos/{owner}/{repo}/actions/runs",
                key="workflow_runs",
                params={"head_sha": pull_request["headRefOid"]},
            )
        return runs

    keep_ready_during_rerun = not missing_checks and not workflows
    for check in (item for item in required_checks if not succeeded(item)):
        if check["__typename"] != "CheckRun" or check.get("status") == "COMPLETED":
            keep_ready_during_rerun = False
            break
        if any(check_key(previous) == check_key(check) and succeeded(previous) for previous in checks):
            continue
        current_run = (check.get("checkSuite") or {}).get("workflowRun") or {}
        history = load_runs() if current_run.get("databaseId") else []
        workflow = current_run.get("workflow") or {}
        if any(run.get("head_sha") == pull_request["headRefOid"]
               and run["id"] < current_run["databaseId"]
               and run.get("workflow_id") == workflow.get("databaseId")
               and run.get("status") == "completed" and run.get("conclusion") == "success"
               and any(pr.get("number") == pull_request["number"] for pr in run.get("pull_requests", []))
               for run in history):
            continue
        rerun = next((run for run in history
                      if run["id"] == current_run.get("databaseId") and run.get("run_attempt", 1) > 1), None)
        if rerun:
            attempt_key = f"attempt:{rerun['id']}:{rerun['run_attempt'] - 1}"
            if attempt_key not in rules_cache:
                rules_cache[attempt_key] = github.request(
                    "GET",
                    f"/repos/{owner}/{repo}/actions/runs/{rerun['id']}/attempts/{rerun['run_attempt'] - 1}",
                )
            attempt = rules_cache[attempt_key]
            if (attempt.get("head_sha") == pull_request["headRefOid"]
                    and attempt.get("status") == "completed" and attempt.get("conclusion") == "success"):
                continue
        keep_ready_during_rerun = False
        break

    if workflows:
        all_runs = load_runs()
        for workflow in workflows:
            source_key = f"workflow-repository:{workflow['repository_id']}"
            if source_key not in rules_cache:
                rules_cache[source_key] = github.request("GET", f"/repositories/{workflow['repository_id']}")
            source = rules_cache[source_key]
            source_path = f"{source['full_name']}/{workflow['path']}"
            version = workflow.get("sha") or workflow.get("ref") or f"refs/heads/{source['default_branch']}"
            versions = {version, re.sub(r"^refs/(?:heads|tags)/", "", version)}

            def matches(run):
                if (run.get("head_sha") != pull_request["headRefOid"]
                        or run.get("event") not in {"pull_request", "pull_request_target", "merge_group"}
                        or not any(pr.get("number") == pull_request["number"] for pr in run.get("pull_requests", []))):
                    return False
                path, _, ref = (run.get("path") or "").partition("@")
                repository = run.get("repository") or {}
                return ref in versions and (path == source_path or
                    repository.get("id") == workflow["repository_id"] and path == workflow["path"])

            matching = sorted((run for run in all_runs if matches(run)), key=lambda run: run["id"], reverse=True)
            latest_run = matching[0] if matching else None
            check_items.append({
                "name": f"Сценарий {workflow['path']}",
                "done": bool(latest_run and latest_run.get("status") == "completed"
                             and latest_run.get("conclusion") == "success"),
                "result": ((latest_run or {}).get("conclusion") or (latest_run or {}).get("status") or "EXPECTED").upper(),
            })

    report_checks = sorted((check for check in checks
                            if ((check.get("checkSuite") or {}).get("app") or {}).get("slug") == report_app_slug
                            and (check.get("externalId") == f"auto-draft:{pull_request['number']}"
                                 or (check.get("externalId") or "").startswith(f"auto-draft:{pull_request['number']}:"))),
                           key=lambda check: check["databaseId"], reverse=True)
    report_check = None
    if report_checks:
        check = report_checks[0]
        report_check = {
            "id": check["databaseId"],
            "name": check["name"],
            "external_id": check.get("externalId"),
            "conclusion": (check.get("conclusion") or "").lower() or None,
        }
    return {
        "comments": comments,
        "report_check": report_check,
        "checks_ready": all(item["done"] for item in check_items),
        "code_rabbit_ready": code_rabbit_ready,
        "code_rabbit_reviewed": code_rabbit_reviewed,
        "code_rabbit_absent": code_rabbit_absent,
        "code_rabbit_timed_out": timed_out,
        "code_rabbit_unavailable": code_rabbit_unavailable,
        "code_rabbit_wait_minutes": CODE_RABBIT_WAIT_MINUTES,
        "keep_ready_during_rabbit_rerun": previous_success,
        "keep_ready_during_rerun": keep_ready_during_rerun,
        "rate_limited": rate_limited,
        "pending_checks": [item["name"] for item in check_items if not item["done"]],
        "check_items": check_items,
    }
