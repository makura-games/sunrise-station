"""Минимальный клиент GitHub и чтение артефактов Actions."""

import io
import json
import re
import time
import zipfile
from urllib.parse import urljoin, urlsplit

import requests


class GitHubError(RuntimeError):
    """Ошибка API без ключей доступа и содержимого ответа."""


class GitHub:
    def __init__(self, repository: str, token: str) -> None:
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise ValueError("Неверное имя репозитория GitHub")
        if not token:
            raise ValueError("Не задан служебный ключ GITHUB_TOKEN")
        self.repository = repository
        self.root = f"/repos/{repository}"
        self.session = requests.Session()
        self.session.headers.update(
            {
                "Authorization": f"Bearer {token}",
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": "2022-11-28",
            }
        )

    def request(self, method: str, path: str, **kwargs) -> requests.Response:
        if not path.startswith(self.root + "/") and path != self.root:
            raise ValueError("Запрос выходит за пределы текущего репозитория")
        for attempt in range(4):
            try:
                response = self.session.request(
                    method,
                    "https://api.github.com" + path,
                    timeout=30,
                    allow_redirects=False,
                    **kwargs,
                )
            except (requests.ConnectionError, requests.Timeout):
                if method != "GET" or attempt == 3:
                    raise GitHubError(
                        "GitHub недоступен; состояние не подтверждено"
                    ) from None
            else:
                if response.status_code not in {429, 500, 502, 503, 504}:
                    return response
                if method != "GET":
                    return response
            if attempt == 3:
                break
            time.sleep(2**attempt)
        raise GitHubError("GitHub временно отклоняет запросы; повторите позже")

    def json(self, method: str, path: str, **kwargs):
        response = self.request(method, path, **kwargs)
        if not 200 <= response.status_code < 300:
            if response.status_code in {403, 429} and (
                response.status_code == 429
                or response.headers.get("X-RateLimit-Remaining") == "0"
                or response.headers.get("Retry-After")
            ):
                raise GitHubError(
                    "GitHub ограничил частоту запросов; повторите позже"
                )
            raise GitHubError(
                f"GitHub HTTP {response.status_code}: "
                "проверьте доступ и наличие объекта"
            )
        return response.json() if response.content else None

    def pages(self, path: str, key: str | None = None, **params):
        page = 1
        while True:
            document = self.json(
                "GET", path, params={**params, "per_page": 100, "page": page}
            )
            items = document[key] if key else document
            yield from items
            if len(items) < 100:
                return
            page += 1

    def pull_status(self, number: int) -> dict:
        if type(number) is not int or number <= 0:
            raise ValueError("Некорректный номер PR")
        owner, name = self.repository.split("/")
        query = (
            "query($owner:String!,$name:String!,$number:Int!){"
            "repository(owner:$owner,name:$name){pullRequest(number:$number){"
            "state isDraft reviewDecision mergeable}}}"
        )
        try:
            response = self.session.post(
                "https://api.github.com/graphql",
                json={
                    "query": query,
                    "variables": {
                        "owner": owner,
                        "name": name,
                        "number": number,
                    },
                },
                timeout=30,
                allow_redirects=False,
            )
            if response.status_code != 200:
                raise GitHubError(
                    f"Статус PR: GitHub HTTP {response.status_code}"
                )
            result = response.json()
            if result.get("errors"):
                raise GitHubError("GitHub не предоставил общий статус ревью")
            status = result["data"]["repository"]["pullRequest"]
            if not isinstance(status, dict):
                raise GitHubError("Статус PR отсутствует в ответе GitHub")
            return status
        except (requests.RequestException, ValueError, KeyError, TypeError):
            raise GitHubError(
                "Не удалось получить статус PR из GitHub"
            ) from None

    def event_artifact(self, run_id: int) -> dict:
        artifacts = list(
            self.pages(
                f"{self.root}/actions/runs/{run_id}/artifacts", "artifacts"
            )
        )
        artifacts = [
            item for item in artifacts if item["name"] == "discord-event"
        ]
        if len(artifacts) != 1 or artifacts[0]["expired"]:
            raise GitHubError(
                "Файл события отсутствует или истёк срок хранения"
            )
        return self.artifact_json(artifacts[0]["id"], "event.json")

    def artifact_json(self, artifact_id: int, filename: str) -> dict:
        response = self.request(
            "GET", f"{self.root}/actions/artifacts/{artifact_id}/zip"
        )
        if response.status_code != 302:
            raise GitHubError("GitHub не предоставил файл события")
        address = response.headers.get("Location", "")
        try:
            for redirect in range(6):
                parsed = urlsplit(address)
                if (
                    parsed.scheme != "https"
                    or not parsed.hostname
                    or parsed.username is not None
                    or parsed.password is not None
                    or re.search(r"[\s\x00-\x1f\x7f]", address)
                ):
                    raise GitHubError("Небезопасный адрес хранилища GitHub")
                with requests.get(
                    address, stream=True, timeout=30, allow_redirects=False
                ) as download:
                    if download.status_code in {301, 302, 303, 307, 308}:
                        location = download.headers.get("Location")
                        if not location:
                            raise GitHubError("Нет адреса перенаправления")
                        if re.search(r"[\s\x00-\x1f\x7f]", location):
                            raise GitHubError("Небезопасное перенаправление")
                        if redirect == 5:
                            raise GitHubError("Слишком много перенаправлений")
                        address = (
                            location
                            if urlsplit(location).scheme
                            else urljoin(address, location)
                        )
                        continue
                    if download.status_code != 200:
                        raise GitHubError("Не удалось скачать файл события")
                    archive = io.BytesIO()
                    for chunk in download.iter_content(65536):
                        archive.write(chunk)
                        if archive.tell() > 32 * 1024 * 1024:
                            raise GitHubError("Файл события превышает 32 МБ")
                    break
        except ValueError:
            raise GitHubError("Небезопасный адрес хранилища GitHub") from None
        except requests.RequestException:
            raise GitHubError(
                "Хранилище событий временно недоступно"
            ) from None
        try:
            with zipfile.ZipFile(archive) as bundle:
                items = bundle.infolist()
                if len(items) != 1 or items[0].filename != filename:
                    raise GitHubError(f"Ожидался единственный файл {filename}")
                if items[0].file_size > 26 * 1024 * 1024:
                    raise GitHubError("Распакованное событие слишком велико")
                return json.loads(bundle.read(items[0]))
        except (zipfile.BadZipFile, RuntimeError, ValueError) as error:
            raise GitHubError(
                "Архив события повреждён или нечитаем"
            ) from error
