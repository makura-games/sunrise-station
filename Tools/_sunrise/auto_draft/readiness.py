import os
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
      createdAt
      reviews(first: 1, author: "coderabbitai[bot]") { nodes { author { login __typename } } }
      commits(last: 1) {
        nodes { commit { statusCheckRollup {
          contexts(first: 100, after: $cursor) {
            pageInfo { hasNextPage endCursor }
            nodes {
              __typename
              ... on CheckRun {
                databaseId name status conclusion title externalId
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
    r"(?:disabled|not enabled)|(?:cannot|can't|unable to)\s+(?:perform\s+)?(?:an?\s+)?review\b",
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
        return check.get("state") != "PENDING"
    return check.get("status") == "COMPLETED"


def load_readiness(*, github, owner, repo, pull_request, rules_cache, comments_github=None,
                   report_app_slug="github-actions", now=None):
    comments_github = comments_github or github
    now = time.time() if now is None else now
    checks = []
    cursor = None
    created_at = None
    has_rabbit_review = False
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
        created_at = current["createdAt"]
        has_rabbit_review = any((review.get("author") or {}).get("__typename") == "Bot"
                                for review in current.get("reviews", {}).get("nodes", []))
        commits = current.get("commits", {}).get("nodes", [])
        connection = (((commits[0].get("commit") or {}).get("statusCheckRollup") or {}).get("contexts")
                      if commits else None)
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
    active_rabbit_checks = [check for check in rabbit_checks if not is_unavailable_rabbit_check(check)]
    reviewed = any(succeeded(check) and re.match(r"^Review completed\b", check.get("description") or check.get("title") or "", re.I)
                   for check in rabbit_checks)

    comments = comments_github.paginate(f"/repos/{owner}/{repo}/issues/{pull_request['number']}/comments")
    rabbit_comments = [comment for comment in comments
                       if (comment.get("user") or {}).get("type") == "Bot"
                       and (comment.get("user") or {}).get("login") == "coderabbitai[bot]"]
    code_rabbit_wait_minutes = 10
    has_rabbit_activity = any(not REVIEW_UNAVAILABLE.search(comment.get("body") or "")
                              for comment in rabbit_comments)
    code_rabbit_absent = (not active_rabbit_checks and not has_rabbit_review and not has_rabbit_activity
                          and now - timestamp(created_at) >= code_rabbit_wait_minutes * 60)

    rate_limited = False
    if not reviewed and os.getenv("AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT") != "false":
        rate_limited = any(MENTIONS_LIMIT.search(check.get("description") or check.get("title") or "")
                           for check in rabbit_checks)
        if not rate_limited:
            for comment in rabbit_comments:
                if not MENTIONS_LIMIT.search(comment.get("body") or ""):
                    continue
                newer_pending = any(
                    (check.get("state") == "PENDING" or check.get("status") in {"QUEUED", "IN_PROGRESS", "PENDING"})
                    and timestamp(check.get("createdAt")) > timestamp(comment.get("updated_at"))
                    for check in rabbit_checks
                )
                if not newer_pending:
                    rate_limited = True
                    break
    code_rabbit_ready = code_rabbit_absent or reviewed or rate_limited

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
        "code_rabbit_absent": code_rabbit_absent,
        "code_rabbit_wait_minutes": code_rabbit_wait_minutes,
        "keep_ready_during_rerun": keep_ready_during_rerun,
        "rate_limited": rate_limited,
        "pending_checks": [item["name"] for item in check_items if not item["done"]],
        "check_items": check_items,
    }
