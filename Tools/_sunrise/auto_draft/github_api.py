import json
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


class GitHubError(RuntimeError):
    def __init__(self, message, status=None, headers=None):
        super().__init__(message)
        self.status = status
        self.headers = {key.lower(): value for key, value in (headers or {}).items()}


class GitHub:
    def __init__(self, token, api_url="https://api.github.com"):
        self.token = token
        self.api_url = api_url.rstrip("/")

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
            with urlopen(request, timeout=30) as response:
                raw = response.read()
                return (json.loads(raw) if raw else None), response.headers
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
        result = self.request("POST", "/graphql", {"query": query, "variables": variables})
        if result.get("errors"):
            raise GitHubError("; ".join(error["message"] for error in result["errors"]))
        return result["data"]

    def paginate(self, path, *, key=None, params=None):
        items = []
        page = 1
        while True:
            data, headers = self._request("GET", path, params={**(params or {}), "per_page": 100, "page": page})
            current = data[key] if key else data
            items.extend(current)
            if 'rel="next"' not in headers.get("Link", ""):
                return items
            page += 1
