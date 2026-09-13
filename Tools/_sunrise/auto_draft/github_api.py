import json
from urllib.error import HTTPError
from urllib.parse import urlencode, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener


class GitHubError(RuntimeError):
    def __init__(self, message, status=None, headers=None):
        super().__init__(message)
        self.status = status
        self.headers = {key.lower(): value for key, value in (headers or {}).items()}


class HTTPSRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, new_url):
        source = urlsplit(request.full_url)
        target = urlsplit(new_url)
        if target.scheme.lower() != "https":
            raise GitHubError("GitHub API отклонил перенаправление не на HTTPS.")

        redirected = super().redirect_request(request, file, code, message, headers, new_url)
        if redirected and (source.scheme.lower(), source.netloc.lower()) != (
                target.scheme.lower(), target.netloc.lower()):
            redirected.remove_header("Authorization")
        return redirected


class GitHub:
    def __init__(self, token, api_url="https://api.github.com"):
        self.token = token
        if urlsplit(api_url).scheme != "https":
            raise ValueError("GitHub API должен использовать HTTPS.")
        self.api_url = api_url.rstrip("/")
        self._opener = build_opener(HTTPSRedirectHandler())

    def _request(self, method, path, body=None, params=None):
        query = f"?{urlencode(params)}" if params else ""
        payload = None if body is None else json.dumps(body).encode()
        request = Request(
            f"{self.api_url}{path}{query}",
            data=payload,
            method=method,
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self.token}",
                "Content-Type": "application/json",
                "User-Agent": "sunrise-auto-draft",
                "X-GitHub-Api-Version": "2022-11-28",
            },
        )
        try:
            with self._opener.open(request, timeout=30) as response:
                raw = response.read()
                return (json.loads(raw) if raw else None), response.status, response.headers
        except HTTPError as error:
            raw = error.read()
            try:
                message = json.loads(raw).get("message", raw.decode(errors="replace"))
            except json.JSONDecodeError:
                message = raw.decode(errors="replace")
            raise GitHubError(message or str(error), error.code, error.headers) from error

    def request(self, method, path, body=None, params=None):
        return self._request(method, path, body, params)[0]

    def graphql(self, query, variables):
        result, status, headers = self._request(
            "POST", "/graphql", {"query": query, "variables": variables}
        )
        if result is None:
            raise GitHubError("GitHub GraphQL вернул пустой ответ.", status, headers)
        if result.get("errors"):
            errors = result["errors"]
            if any(error.get("type") == "RATE_LIMITED" for error in errors):
                status = 429
            raise GitHubError("; ".join(error["message"] for error in errors), status, headers)
        return result["data"]

    def paginate(self, path, *, key=None, params=None):
        items = []
        page = 1
        while True:
            data, _, headers = self._request(
                "GET", path, params={**(params or {}), "per_page": 100, "page": page}
            )
            current = data[key] if key else data
            items.extend(current)
            if 'rel="next"' not in headers.get("Link", ""):
                return items
            page += 1
