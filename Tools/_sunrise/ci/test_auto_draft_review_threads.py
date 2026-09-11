import json
import re
import subprocess
import textwrap
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "sunrise-auto-draft-review-threads.yml"
SIGNAL_WORKFLOW_PATH = (
    REPO_ROOT / ".github" / "workflows" / "sunrise-auto-draft-review-state-changed.yml"
)
CODERABBIT_PATH = REPO_ROOT / ".coderabbit.yaml"
SCRIPT_PATH = REPO_ROOT / "Tools" / "_sunrise" / "auto_draft" / "review_threads.js"


class AutoDraftReviewThreadsWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.signal_workflow = SIGNAL_WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.coderabbit = CODERABBIT_PATH.read_text(encoding="utf-8")
        cls.script = SCRIPT_PATH.read_text(encoding="utf-8")

    def test_review_events_are_relayed_without_privileged_operations(self):
        self.assertIn('name: "PR: Automatic Draft Management - Review Events"', self.signal_workflow)
        self.assertRegex(
            self.signal_workflow,
            r"pull_request_review:\s+types: \[submitted, edited, dismissed\]",
        )
        self.assertNotIn("secrets.", self.signal_workflow)
        self.assertNotIn("actions/checkout", self.signal_workflow)
        self.assertNotIn("actions/github-script", self.signal_workflow)

        self.assertIn("workflow_run:", self.workflow)
        self.assertIn('workflows: ["PR: Automatic Draft Management - Review Events", "Build & Test Debug", "YAML Linter"]', self.workflow)
        self.assertNotIn("pull_request_review_comment:", self.workflow)
        self.assertNotRegex(self.workflow, r"(?m)^  pull_request_review:\s*$")

    def test_privileged_workflow_uses_organization_app_token(self):
        self.assertIn("uses: actions/create-github-app-token@v3", self.workflow)
        self.assertIn("client-id: ${{ vars.AUTO_DRAFT_APP_CLIENT_ID }}", self.workflow)
        self.assertIn(
            "private-key: ${{ secrets.AUTO_DRAFT_APP_PRIVATE_KEY }}",
            self.workflow,
        )
        self.assertIn("permission-contents: write", self.workflow)
        self.assertIn("permission-issues: write", self.workflow)
        self.assertIn("permission-pull-requests: write", self.workflow)
        self.assertIn("permission-actions: read", self.workflow)
        self.assertIn("permission-checks: write", self.workflow)
        self.assertIn("permission-statuses: read", self.workflow)
        self.assertIn("uses: actions/checkout@v6", self.workflow)
        self.assertIn("ref: ${{ github.workflow_sha }}", self.workflow)
        self.assertIn("sparse-checkout: Tools/_sunrise/auto_draft", self.workflow)
        self.assertIn("persist-credentials: false", self.workflow)
        self.assertIn("github-token: ${{ steps.app-token.outputs.token }}", self.workflow)
        self.assertNotIn("AUTO_DRAFT_TOKEN", self.workflow)
        self.assertNotIn("secrets.GITHUB_TOKEN", self.workflow)
        self.assertNotIn("continue-on-error", self.workflow)
        self.assertIn("const failures = [];", self.script)
        self.assertIn("for (const number of numbers)", self.script)
        self.assertRegex(self.workflow, r"(?m)^      group: \$\{\{ github.workflow \}\}$")
        self.assertNotRegex(self.workflow, r"(?m)^concurrency:")
        self.assertIn("cancel-in-progress: false", self.workflow)
        self.assertIn("queue: max", self.workflow)
        self.assertIn("AUTO_DRAFT_APP_SLUG: ${{ steps.app-token.outputs.app-slug }}", self.workflow)
        self.assertNotIn("AUTO_DRAFT_READ_TOKEN", self.workflow)
        self.assertIn("AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT", self.workflow)
        self.assertIn("checks: write", self.workflow)
        self.assertIn("statuses: read", self.workflow)
        self.assertIn("permissions: {}", self.workflow)
        self.assertIn("await run({ github, context, core })", self.workflow)

    def test_review_state_comes_from_regular_github_reviews(self):
        for expected in (
            "latestOpinionatedReviews",
            "authorCanPushToRepository",
            "pullRequestReview",
            "READY_FOR_REVIEW_EVENT",
        ):
            self.assertIn(expected, self.script)

        for obsolete in (
            "CODEOWNERS",
            "AUTO_DRAFT_COMMENT_MARKER",
            "createComment",
            "updateComment",
        ):
            self.assertNotIn(obsolete, self.script)

    def test_coderabbit_submits_real_review_decisions(self):
        self.assertRegex(
            self.coderabbit,
            r"(?m)^  request_changes_workflow: true$",
        )
        self.assertRegex(self.coderabbit, r"auto_review:\s+drafts: true")

    def test_policy_scenarios(self):
        match = re.search(
            r"^\s*// AUTO_DRAFT_POLICY_START\s*$\n"
            r"(?P<policy>.*?)"
            r"^\s*// AUTO_DRAFT_POLICY_END\s*$",
            self.script,
            re.DOTALL | re.MULTILINE,
        )
        self.assertIsNotNone(match, "Не найдены стабильные маркеры функции политики")
        policy = textwrap.dedent(match.group("policy"))
        cases = [
            {
                "name": "готовый ПР не закрывается на повторном запуске успешных проверок того же коммита",
                "input": self.policy_input(checksReady=False, keepReadyDuringRerun=True),
                "expected": "keep",
            },
            {
                "name": "черновик ждёт завершения повторного запуска даже после прежнего успеха",
                "input": self.policy_input(isDraft=True, hasMarker=True, checksReady=False, keepReadyDuringRerun=True),
                "expected": "keep",
            },
            {
                "name": "старый успех тестов не отменяет открытые замечания",
                "input": self.policy_input(latestBlockingAt=10, checksReady=False, keepReadyDuringRerun=True),
                "expected": "draft",
            },
            {
                "name": "новое требование включает draft",
                "input": self.policy_input(latestBlockingAt=20),
                "expected": "draft",
            },
            {
                "name": "закрытие всех обсуждений снимает автодрафт",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                    allBlockingThreadsResolved=True,
                ),
                "expected": "ready",
            },
            {
                "name": "частично закрытые обсуждения сохраняют draft",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                ),
                "expected": "keep",
            },
            {
                "name": "требование без обсуждений сохраняет draft",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                ),
                "expected": "keep",
            },
            {
                "name": "ручной Ready удаляет служебную метку",
                "input": self.policy_input(
                    hasMarker=True,
                    latestBlockingAt=10,
                    latestReadyAt=20,
                ),
                "expected": "cleanup",
            },
            {
                "name": "новое требование после ручного Ready снова включает draft",
                "input": self.policy_input(
                    latestBlockingAt=30,
                    latestReadyAt=20,
                ),
                "expected": "draft",
            },
            {
                "name": "ручной draft без метки не переводится в Ready",
                "input": self.policy_input(isDraft=True),
                "expected": "keep",
            },
            {
                "name": "снятое требование убирает автодрафт",
                "input": self.policy_input(isDraft=True, hasMarker=True),
                "expected": "ready",
            },
            {
                "name": "обычный комментарий ничего не меняет",
                "input": self.policy_input(),
                "expected": "keep",
            },
            {
                "name": "первое ревью ещё не закончено",
                "input": self.policy_input(codeRabbitReady=False),
                "expected": "draft",
            },
            {
                "name": "новый коммит ещё не прошёл обязательные проверки",
                "input": self.policy_input(checksReady=False),
                "expected": "draft",
            },
            {
                "name": "закрытие обсуждений не отменяет ожидание повторного ревью",
                "input": self.policy_input(isDraft=True, hasMarker=True,
                    latestBlockingAt=10, allBlockingThreadsResolved=True, codeRabbitReady=False),
                "expected": "keep",
            },
            {
                "name": "ручной Ready остаётся аварийным обходом проверок",
                "input": self.policy_input(latestReadyAt=20, checksReady=False, codeRabbitReady=False),
                "expected": "keep",
            },
        ]
        harness = f"""
{policy}
const cases = {json.dumps(cases, ensure_ascii=False)};
for (const testCase of cases) {{
  const actual = decideDraftState(testCase.input);
  if (actual !== testCase.expected) {{
    console.error(`${{testCase.name}}: ожидалось ${{testCase.expected}}, получено ${{actual}}`);
    process.exitCode = 1;
  }}
}}
"""
        result = subprocess.run(
            ["node", "-"],
            input=harness,
            text=True,
            capture_output=True,
            encoding="utf-8",
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)

    @staticmethod
    def policy_input(**overrides):
        defaults = {
            "isDraft": False,
            "hasMarker": False,
            "latestBlockingAt": None,
            "latestReadyAt": None,
            "allBlockingThreadsResolved": False,
            "checksReady": True,
            "codeRabbitReady": True,
        }
        defaults.update(overrides)
        return defaults

    def test_runtime_pagination_events_and_recovery(self):
        harness = r"""
const assert = require('node:assert/strict');
const run = require('./Tools/_sunrise/auto_draft/review_threads.js');
const marker = require('./Tools/_sunrise/auto_draft/config.json').label.name;
process.env.AUTO_DRAFT_APP_SLUG = 'auto-draft-app';
const review = { id: 'R1', state: 'CHANGES_REQUESTED',
  submittedAt: '2026-09-01T00:00:00Z', authorCanPushToRepository: true,
  author: { login: 'alice' } };
const thread = { isResolved: false,
  comments: { nodes: [{ pullRequestReview: { id: 'R1' } }] } };
function pull(overrides = {}) {
  return { id: 'PR1', number: 1, state: 'OPEN', isDraft: false,
    headRefOid: '1234567890abcdef1234567890abcdef12345678', baseRefName: 'master',
    labels: [], latestOpinionatedReviews: [review], reviewThreads: [thread],
    timelineItems: { nodes: [] }, ...overrides };
}
function mock(pulls, fail = '') {
  const actions = [], queries = [], scanned = [];
  const github = {
    paginate: async method => {
      if (typeof method === 'string') return [];
      if (method === github.rest.issues.listComments || method === github.rest.checks.listForRef) return [];
      scanned.push(true); return pulls;
    },
    rest: {
      checks: { listForRef() {}, async create() {}, async update() {} },
      pulls: { list() {}, async get({ pull_number }) {
        const pr = pulls.find(pr => pr.number === pull_number);
        return { data: { head: { sha: pr.headRefOid }, base: { ref: pr.baseRefName }, draft: pr.isDraft, state: 'open' } };
      } },
      repos: { listPullRequestsAssociatedWithCommit() {}, async getBranch() { return { data: {} }; } },
      issues: {
        async getLabel() { return { data: require('./Tools/_sunrise/auto_draft/config.json').label }; },
        async createLabel() {},
        async updateLabel() {},
        listComments() {},
        async createComment() {},
        async updateComment() {},
        async deleteComment() {},
        async addLabels({ issue_number, labels }) {
          actions.push('add:' + issue_number);
          if (fail === 'add') throw new Error('add failed');
          pulls.find(pr => pr.number === issue_number).labels.push(...labels.map(name => ({ name })));
        },
        async removeLabel({ issue_number, name }) {
          const pr = pulls.find(pr => pr.number === issue_number);
          if (!pr.labels.some(label => label.name === name))
            throw Object.assign(new Error('not found'), { status: 404 });
          actions.push('remove:' + issue_number);
          if (fail === 'remove') throw new Error('remove failed');
          pr.labels = pr.labels.filter(label => label.name !== name);
        },
      },
    },
    async graphql(query, variables) {
      if (query.trimStart().startsWith('mutation')) {
        const action = query.includes('convertPullRequestToDraft') ? 'draft' : 'ready';
        actions.push(action);
        if (fail === action) throw new Error(action + ' failed');
        const pr = pulls.find(pr => pr.id === variables.id);
        pr.isDraft = action === 'draft';
        if (action === 'ready')
          pr.timelineItems.nodes = [{ createdAt: '2026-09-02T00:00:00Z' }];
        return {};
      }
      queries.push(variables);
      if (fail === 'query') throw new Error('query failed');
      const pr = pulls.find(pr => pr.number === variables.number);
      if (!pr) return { repository: { pullRequest: null } };
      if (query.includes('query Readiness')) {
        return { repository: { pullRequest: { ...pr, baseRef: null,
          commits: { nodes: [{ commit: { statusCheckRollup: { contexts: {
            pageInfo: { hasNextPage: false, endCursor: null }, nodes: [{
              __typename: 'StatusContext', context: 'CodeRabbit', state: 'SUCCESS',
              description: 'Review completed', createdAt: '2026-09-03T00:00:00Z',
              creator: { __typename: 'Bot', login: 'coderabbitai' }, isRequired: false,
            }],
          } } } }] },
        } } };
      }
      const result = { ...pr };
      for (const field of ['labels', 'latestOpinionatedReviews', 'reviewThreads']) {
        if (!variables['load' + field]) { delete result[field]; continue; }
        const start = Number(variables[field + 'Cursor'] || 0);
        const end = start + 100;
        result[field] = { nodes: structuredClone(pr[field].slice(start, end)),
          pageInfo: { hasNextPage: end < pr[field].length, endCursor: String(end) } };
      }
      return { repository: { pullRequest: result } };
    },
  };
  return { github, actions, queries, scanned,
    context: { repo: { owner: 'example', repo: 'repo' }, eventName: 'workflow_dispatch',
      payload: { inputs: { 'pr-number': '1' } } },
    core: { info() {}, warning() {}, error() {} } };
}
(async () => {
  let env = mock([pull()]);
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  // Старое незакрытое требование того же автора сохраняется между проходами ревью.
  const oldThread = { ...thread, comments: { nodes: [{ pullRequestReview: {
    id: 'OLD', state: 'CHANGES_REQUESTED', author: { login: 'alice' },
  } }] } };
  env = mock([pull({ isDraft: true, labels: [{ name: marker }],
    reviewThreads: [{ ...thread, isResolved: true }, oldThread] })]);
  await run(env);
  assert.deepEqual(env.actions, []);

  env = mock([pull({ isDraft: true, labels: [{ name: marker }],
    reviewThreads: [{ ...thread, isResolved: true }, { ...oldThread, isResolved: true }] })]);
  await run(env);
  assert.deepEqual(env.actions, ['ready', 'remove:1']);

  // Подмена клиента проверок для диагностики не меняет клиент операций над ПР.
  env = mock([pull({ latestOpinionatedReviews: [] })]);
  const originalGraphql = env.github.graphql;
  let readinessReads = 0;
  env.readGithub = { ...env.github, graphql: async (query, variables) => {
    assert.ok(query.includes('query Readiness'));
    readinessReads++;
    return originalGraphql(query, variables);
  } };
  env.github.graphql = async (query, variables) => {
    assert.ok(!query.includes('query Readiness'));
    return originalGraphql(query, variables);
  };
  await run(env);
  assert.equal(readinessReads, 1);
  assert.deepEqual(env.actions, []);

  // Обычный комментарий прежнего прохода не становится требованием исправлений.
  env = mock([pull()]);
  env.readGithub = { ...env.github, graphql: async () => { throw new Error('read unavailable'); } };
  await assert.rejects(run(env), /read unavailable/);
  assert.deepEqual(env.actions, []);

  // Исправления списка человеком запускают синхронизацию, собственные изменения бота — нет.
  for (const action of ['edited', 'deleted', 'created']) {
    for (const sender of ['maintainer', 'auto-draft-app[bot]']) {
      env = mock([pull()]);
      env.context.eventName = 'issue_comment';
      env.context.payload = { action, issue: { number: 1, pull_request: {} },
        comment: { user: { type: 'Bot', login: 'auto-draft-app[bot]' } },
        sender: { login: sender, type: sender === 'maintainer' ? 'User' : 'Bot' } };
      await run(env);
      assert.deepEqual(env.actions, action !== 'created' && sender === 'maintainer' ? ['add:1', 'draft'] : []);
    }
  }

  // Обычный комментарий прежнего прохода не становится требованием исправлений.
  const commentThread = structuredClone(oldThread);
  commentThread.comments.nodes[0].pullRequestReview.state = 'COMMENTED';
  env = mock([pull({ isDraft: true, labels: [{ name: marker }],
    reviewThreads: [{ ...thread, isResolved: true }, commentThread] })]);
  await run(env);
  assert.deepEqual(env.actions, ['ready', 'remove:1']);

  // Ручной черновик или новый коммит, появившиеся во время запроса, не перезаписываются.
  for (const change of ['draft', 'head']) {
    env = mock([pull()]);
    env.github.rest.pulls.get = async () => ({ data: { draft: change === 'draft',
      head: { sha: change === 'head' ? 'new-head' : pull().headRefOid }, base: { ref: 'master' } } });
    await run(env);
    assert.deepEqual(env.actions, []);
  }

  env = mock([pull({ reviewThreads: [] })]);
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  env = mock([pull({ isDraft: true, labels: [{ name: marker }], reviewThreads: [] })]);
  await run(env);
  assert.deepEqual(env.actions, []);

  // Одобрение Боба не отменяет незакрытое требование Алисы, независимо от времени.
  const bobApproval = { ...review, id: 'R2', state: 'APPROVED',
    submittedAt: '2026-09-02T00:00:00Z', author: { login: 'bob' } };
  const reviewed = pull({ isDraft: true, labels: [{ name: marker }],
    latestOpinionatedReviews: [review, bobApproval] });
  env = mock([reviewed]);
  await run(env);
  assert.deepEqual(env.actions, []);

  env = mock([pull({ latestOpinionatedReviews: [review, bobApproval] })]);
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  // GitHub возвращает последнее решение каждого автора: Алиса сама одобрила ПР.
  reviewed.latestOpinionatedReviews = [
    { ...review, id: 'R3', state: 'APPROVED', submittedAt: '2026-09-03T00:00:00Z' },
    bobApproval,
  ];
  env = mock([reviewed]);
  await run(env);
  assert.deepEqual(env.actions, ['ready', 'remove:1']);

  // Повторное открытие обсуждения после автоматического Ready снова блокирует ПР.
  env = mock([pull({ timelineItems: { nodes: [{ createdAt: '2026-09-02T00:00:00Z',
    actor: { login: 'auto-draft-app' } }] } })]);
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  env = mock([pull({ timelineItems: { nodes: [{ createdAt: '2026-09-02T00:00:00Z',
    actor: { login: 'maintainer' } }] } })]);
  await run(env);
  assert.deepEqual(env.actions, []);

  // Первые сто обсуждений закрыты, но на следующей странице осталось замечание.
  env = mock([pull({ isDraft: true, labels: [{ name: marker }],
    reviewThreads: [...Array.from({ length: 100 }, () => ({ ...thread, isResolved: true })), thread] })]);
  await run(env);
  assert.deepEqual(env.actions, []);
  assert.equal(env.queries.length, 3);
  assert.equal(env.queries[1].reviewThreadsCursor, '100');
  assert.equal(env.queries[1].loadlabels, false);
  assert.equal(env.queries[1].loadlatestOpinionatedReviews, false);

  // Блокирующее ревью тоже может оказаться за пределами первой страницы.
  env = mock([pull({ latestOpinionatedReviews: [
    ...Array.from({ length: 100 }, (_, i) => ({ ...review, id: 'other' + i,
      state: 'APPROVED', submittedAt: '2026-08-01T00:00:00Z' })), review] })]);
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);
  assert.equal(env.queries[1].latestOpinionatedReviewsCursor, '100');

  // Метка на следующей странице не должна превращать автодрафт в ручной черновик.
  env = mock([pull({ isDraft: true, latestOpinionatedReviews: [], labels: [
    ...Array.from({ length: 100 }, (_, i) => ({ name: 'label' + i })), { name: marker }] })]);
  await run(env);
  assert.deepEqual(env.actions, ['ready', 'remove:1']);

  const ready = () => pull({ isDraft: true, labels: [{ name: marker }],
    reviewThreads: [{ ...thread, isResolved: true }] });
  env = mock([ready()], 'ready');
  await assert.rejects(run(env), /ready failed/);
  assert.deepEqual(env.actions, ['ready']);

  const retry = ready();
  env = mock([retry], 'remove');
  await assert.rejects(run(env), /remove failed/);
  assert.equal(retry.isDraft, false);
  assert.equal(retry.labels[0].name, marker);
  env = mock([retry]);
  await run(env);
  assert.deepEqual(env.actions, ['remove:1']);

  env = mock([pull()], 'draft');
  await assert.rejects(run(env), /draft failed/);
  assert.deepEqual(env.actions, ['add:1', 'draft', 'remove:1']);

  env = mock([pull()], 'query');
  await assert.rejects(run(env), /query failed/);
  assert.deepEqual(env.actions, []);

  env = mock([pull({ latestOpinionatedReviews: [{ ...review, authorCanPushToRepository: false }] })]);
  await run(env);
  assert.deepEqual(env.actions, []);

  env = mock([pull()]);
  env.context.eventName = 'workflow_run';
  env.context.payload = { workflow_run: { conclusion: 'success', pull_requests: [] } };
  await run(env);
  assert.equal(env.scanned.length, 1);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  env = mock([pull()]);
  env.context.eventName = 'workflow_run';
  env.context.payload = { workflow_run: { conclusion: 'success', pull_requests: [], head_sha: 'abc123' } };
  const paginateWorkflow = env.github.paginate;
  env.github.paginate = async (method, parameters) => {
    if (method !== env.github.rest.repos.listPullRequestsAssociatedWithCommit)
      return paginateWorkflow(method, parameters);
    assert.equal(method, env.github.rest.repos.listPullRequestsAssociatedWithCommit);
    assert.equal(parameters.commit_sha, 'abc123');
    return [{ number: 1, state: 'open' }, { number: 2, state: 'closed' }];
  };
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);
  assert.equal(env.scanned.length, 0);

  for (const missing of [false, true]) {
    env = mock([pull()]);
    env.context.eventName = 'workflow_run';
    env.context.payload = { workflow_run: { conclusion: 'success', pull_requests: [], head_sha: 'abc123' } };
    const scan = env.github.paginate;
    env.github.paginate = async (method, parameters) => {
      if (method === env.github.rest.repos.listPullRequestsAssociatedWithCommit) {
        if (missing) throw Object.assign(new Error('not found'), { status: 404 });
        return [];
      }
      return scan(method, parameters);
    };
    await run(env);
    assert.equal(env.scanned.length, 1);
    assert.deepEqual(env.actions, ['add:1', 'draft']);
  }

  env = mock([pull()]);
  env.context.eventName = 'workflow_run';
  env.context.payload = { workflow_run: { name: 'PR: Automatic Draft Management - Review Events', conclusion: 'failure' } };
  await run(env);
  assert.equal(env.queries.length, 0);

  env = mock([pull()]);
  env.context.eventName = 'workflow_run';
  env.context.payload = { workflow_run: { name: 'YAML Linter', conclusion: 'failure', pull_requests: [{ number: 1 }] } };
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  for (const login of ['coderabbitai[bot]', 'contributor']) {
    env = mock([pull()]);
    env.context.eventName = 'issue_comment';
    env.context.payload = { issue: { number: 1, pull_request: {} },
      sender: { type: login === 'contributor' ? 'User' : 'Bot', login } };
    await run(env);
    assert.deepEqual(env.actions, login === 'contributor' ? [] : ['add:1', 'draft']);
  }
  env = mock([pull()]);
  env.context.eventName = 'status';
  env.context.payload = { sha: 'current-head' };
  const paginateStatus = env.github.paginate;
  env.github.paginate = async (method, parameters) => {
    if (method !== env.github.rest.repos.listPullRequestsAssociatedWithCommit)
      return paginateStatus(method, parameters);
    assert.equal(method, env.github.rest.repos.listPullRequestsAssociatedWithCommit);
    assert.equal(parameters.commit_sha, 'current-head');
    return [{ number: 1, state: 'open' }];
  };
  await run(env);
  assert.deepEqual(env.actions, ['add:1', 'draft']);

  env = mock([pull(), pull({ id: 'PR2', number: 2, state: 'CLOSED' }),
    pull({ id: 'PR3', number: 3 })], 'draft');
  env.context.payload.inputs = {};
  await assert.rejects(run(env), /Не удалось синхронизировать 2 ПР/);
  assert.deepEqual(env.actions, ['add:1', 'draft', 'remove:1', 'add:3', 'draft', 'remove:3']);

  env = mock([pull()]);
  env.context.payload.inputs['pr-number'] = '9007199254740993';
  await assert.rejects(run(env), /Некорректный номер/);
  assert.equal(env.queries.length, 0);
  // Обезличенные снимки открытых ПР: требование с открытым обсуждением,
  // все обсуждения закрыты при формальном Request changes и ручной черновик.
  const snapshots = require('./Tools/_sunrise/ci/auto_draft_review_snapshots.json');
  for (const [name, snapshot] of Object.entries(snapshots)) {
    env = mock([pull({ ...snapshot })]);
    await run(env);
    assert.deepEqual(env.actions, name === 'a' ? ['add:1', 'draft'] : []);
  }
  // Цвет и имя метки обновляются отдельно от её назначения ПР.
  env = mock([pull()]);
  const config = structuredClone(require('./Tools/_sunrise/auto_draft/config.json'));
  config.label.previousNames = ['старое имя'];
  const labelWrites = [];
  env.config = config;
  env.github.rest.issues.getLabel = async ({ name }) => {
    if (name === config.label.name) throw Object.assign(new Error('missing'), { status: 404 });
    return { data: { name, color: '000000', description: '' } };
  };
  env.github.rest.issues.updateLabel = async params => labelWrites.push(params);
  await run(env);
  assert.equal(labelWrites.length, 1);
  assert.equal(labelWrites[0].name, 'старое имя');
  assert.equal(labelWrites[0].new_name, config.label.name);
  assert.equal(labelWrites[0].color, config.label.color);
  env = mock([pull()]);
  env.config = { label: { ...config.label, color: 'bad-color' } };
  await assert.rejects(run(env), /config.json/);
  assert.deepEqual(env.actions, []);
  // Полный обход не ограничивается первыми ста ПР.
  env = mock(Array.from({ length: 105 }, (_, index) => pull({ id: 'PR' + (index + 1), number: index + 1,
    latestOpinionatedReviews: [], reviewThreads: [] })));
  env.context.payload.inputs = {};
  await run(env);
  assert.equal(env.queries.filter(query => query.loadlabels).length, 105);
  assert.deepEqual(env.actions, []);

  // После ограничения GitHub не продолжаем посылать запросы для остальных ПР.
  env = mock([pull(), pull({ id: 'PR2', number: 2 })]);
  env.context.payload.inputs = {};
  let limitedQueries = 0;
  env.github.graphql = async () => {
    limitedQueries++;
    throw Object.assign(new Error('API rate limit exceeded'), { status: 429 });
  };
  await assert.rejects(run(env), /Не удалось синхронизировать 1 ПР/);
  assert.equal(limitedQueries, 1);
  assert.deepEqual(env.actions, []);
})().catch(error => { console.error(error); process.exitCode = 1; });
"""
        result = subprocess.run(
            ["node", "-"], input=harness, cwd=REPO_ROOT,
            text=True, capture_output=True, encoding="utf-8", check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_current_commit_readiness(self):
        result = subprocess.run(
            ["node", str(Path(__file__).with_name("test_auto_draft_readiness.js"))],
            text=True, capture_output=True, encoding="utf-8", check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)


if __name__ == "__main__":
    unittest.main()
