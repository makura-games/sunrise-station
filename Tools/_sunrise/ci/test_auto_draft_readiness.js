const assert = require('node:assert/strict');
const load = require('../auto_draft/readiness.js');
const head = '1234567890abcdef1234567890abcdef12345678';
const pullRequest = { number: 1, headRefOid: head, baseRefName: 'master', createdAt: '2026-09-01T00:00:00Z' };
const rabbit = { __typename: 'StatusContext', context: 'CodeRabbit', state: 'SUCCESS',
  description: 'Review completed', createdAt: '2026-09-01T00:00:00Z',
  creator: { __typename: 'Bot', login: 'coderabbitai' }, isRequired: false };
const check = { __typename: 'CheckRun', name: 'Tests', databaseId: 1,
  status: 'COMPLETED', conclusion: 'SUCCESS', isRequired: true,
  checkSuite: { app: { databaseId: 15368, slug: 'github-actions' },
    workflowRun: { workflow: { databaseId: 1 } } } };
const requirement = { context: 'Tests', integration_id: 15368 };
const limited = (sha = head) => ({ user: { type: 'Bot', login: 'coderabbitai[bot]' },
  updated_at: '2026-09-01T00:00:00Z',
  body: '<!-- This is an auto-generated comment: summarize by coderabbit.ai -->\n' +
    '<!-- This is an auto-generated comment: rate limited by coderabbit.ai -->\n' +
    '> ## Rate limit exceeded\n' +
    `> Reviewing files that changed from the base of the PR and between abcdef1234567 and ${sha}.\n` +
    '<!-- end of auto-generated comment: rate limited by coderabbit.ai -->',
});

async function inspect({ checks = [check, rabbit], comments = [], requirements = [requirement],
  classic = [], responseHead = head, fail = '', pages = null, rulesCache = new Map(), workflows = [], runs = [],
  now = Date.parse(pullRequest.createdAt) + 60_000, reviews = [], previousAttempt = {} } = {}) {
  const github = {
    rest: { issues: { listComments() {} }, actions: { listWorkflowRunsForRepo() {},
      async getWorkflowRunAttempt() { return { data: previousAttempt }; } },
      repos: { async getBranch() { return { data: { protection: { enabled: true, required_status_checks: {
        checks: classic.map(check => ({ context: check.context, app_id: check.app?.databaseId })),
      } } } }; } } },
    async request() { return { data: { full_name: 'example/repo', default_branch: 'master' } }; },
    async graphql(query, variables) {
      assert.ok(query.includes('isRequired(pullRequestNumber: $number)'));
      if (fail === 'checks') throw new Error('checks denied');
      const index = Number(variables.cursor || 0);
      const nodes = pages ? pages[index] : checks;
      return { repository: { pullRequest: { ...pullRequest, headRefOid: responseHead,
        reviews: { nodes: reviews },
        commits: { nodes: [{ commit: { statusCheckRollup: { contexts: { nodes,
          pageInfo: { hasNextPage: pages && index + 1 < pages.length, endCursor: String(index + 1) },
        } } } }] },
      } } };
    },
    async paginate(method) {
      if (typeof method === 'string') {
        if (fail === 'rules') throw new Error('rules denied');
        return [{ type: 'required_status_checks', parameters: { required_status_checks: requirements } },
          { type: 'workflows', parameters: { workflows } }];
      }
      if (method === github.rest.actions.listWorkflowRunsForRepo) return runs;
      assert.equal(method, github.rest.issues.listComments);
      return comments;
    },
  };
  return load({ github, owner: 'example', repo: 'repo', pullRequest, rulesCache, now });
}

(async () => {
  delete process.env.AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT;
  let result = await inspect();
  assert.equal(result.checksReady, true);
  assert.equal(result.codeRabbitReady, true);

  // Отсутствующие проверки тоже блокируют готовность, включая ещё не созданные задания.
  result = await inspect({ checks: [rabbit] });
  assert.equal(result.checksReady, false);
  assert.deepEqual(result.pendingChecks, ['Tests']);
  result = await inspect({ checks: [rabbit], requirements: [],
    classic: [{ context: 'Classic CI', app: null }] });
  assert.equal(result.checksReady, false);

  for (const conclusion of ['FAILURE', 'CANCELLED', 'TIMED_OUT', null]) {
    result = await inspect({ checks: [{ ...check, conclusion }, rabbit] });
    assert.equal(result.checksReady, false);
  }
  result = await inspect({ checks: [{ ...check, status: 'IN_PROGRESS' }, rabbit] });
  assert.equal(result.checksReady, false);
  for (const conclusion of ['NEUTRAL', 'SKIPPED']) {
    result = await inspect({ checks: [{ ...check, conclusion }, rabbit] });
    assert.equal(result.checksReady, true);
  }

  // Старое падение не перекрывает успешный перезапуск; новый незавершённый запуск перекрывает успех.
  result = await inspect({ checks: [{ ...check, conclusion: 'FAILURE' }, { ...check, databaseId: 2 }, rabbit] });
  assert.equal(result.checksReady, true);
  result = await inspect({ checks: [check, { ...check, databaseId: 2, conclusion: null }, rabbit] });
  assert.equal(result.checksReady, false);
  result = await inspect({ checks: [check, { ...check, databaseId: 2, conclusion: 'FAILURE',
    checkSuite: { ...check.checkSuite, workflowRun: { workflow: { databaseId: 2 } } } }, rabbit] });
  assert.equal(result.checksReady, false);

  // Необязательная проверка и чужое приложение не заменяют обязательную проверку GitHub Actions.
  result = await inspect({ checks: [check, { ...check, name: 'Optional', isRequired: false, conclusion: 'FAILURE' }, rabbit] });
  assert.equal(result.checksReady, true);
  result = await inspect({ checks: [{ ...check, checkSuite: { app: { databaseId: 999 } } }, rabbit] });
  assert.equal(result.checksReady, false);

  result = await inspect({ checks: [check] });
  assert.equal(result.codeRabbitReady, false);
  result = await inspect({ checks: [check, { ...rabbit, state: 'PENDING' }] });
  assert.equal(result.codeRabbitReady, false);
  result = await inspect({ checks: [check, { ...rabbit, description: 'Review skipped' }] });
  assert.equal(result.codeRabbitReady, false);
  result = await inspect({ checks: [check, { ...rabbit, creator: { __typename: 'User', login: 'coderabbitai' } }] });
  assert.equal(result.codeRabbitReady, false);

  // Формат и наличие хеша не обязательны; автором уведомления должен быть настоящий CodeRabbit.
  result = await inspect({ checks: [check], comments: [limited()] });
  assert.equal(result.rateLimited, true);
  assert.equal(result.codeRabbitReady, true);
  result = await inspect({ checks: [check], comments: [limited(head.slice(0, 7))] });
  assert.equal(result.rateLimited, true);
  result = await inspect({ checks: [check, { ...rabbit, description: 'Review skipped' }], comments: [limited()] });
  assert.equal(result.rateLimited, true);
  for (const body of ['Review rate limited.', 'Rate limit exceeded', 'Review limit reached',
    "You've used all free OSS reviews for now.", 'Лимит запросов исчерпан', limited('abcdef1234567').body,
    limited().body.replace(/Reviewing files[^\n]+/, '')]) {
    result = await inspect({ checks: [check], comments: [{ ...limited(), body }] });
    assert.equal(result.codeRabbitReady, true, body);
  }
  for (const comment of [{ ...limited(), user: { type: 'User', login: 'contributor' } },
    { ...limited(), user: { type: 'Bot', login: 'another[bot]' } },
    { ...limited(), body: 'Review in progress' }]) {
    result = await inspect({ checks: [check], comments: [comment] });
    assert.equal(result.codeRabbitReady, false);
  }
  process.env.AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT = 'false';
  result = await inspect({ checks: [check], comments: [limited()] });
  assert.equal(result.codeRabbitReady, false);

  const afterWait = Date.parse(pullRequest.createdAt) + 10 * 60_000;
  result = await inspect({ checks: [check], now: afterWait - 1 });
  assert.equal(result.codeRabbitAbsent, false);
  assert.equal(result.codeRabbitReady, false);
  result = await inspect({ checks: [check], now: afterWait });
  assert.equal(result.codeRabbitAbsent, true);
  assert.equal(result.codeRabbitReady, true);
  result = await inspect({ checks: [{ ...check, conclusion: 'FAILURE' }], now: afterWait });
  assert.equal(result.checksReady, false);
  for (const evidence of [{ checks: [check, { ...rabbit, state: 'PENDING' }] },
    { comments: [{ ...limited(), body: 'Review in progress' }] },
    { reviews: [{ author: { __typename: 'Bot', login: 'coderabbitai' } }] }]) {
    result = await inspect({ checks: [check], now: afterWait, ...evidence });
    assert.equal(result.codeRabbitAbsent, false);
    assert.equal(result.codeRabbitReady, false);
  }

  // Ожидание повторного запуска не закрывает уже готовый ПР; неудача или новый коммит закрывают.
  result = await inspect({ checks: [check, { ...check, databaseId: 2, status: 'IN_PROGRESS', conclusion: null }, rabbit] });
  assert.equal(result.checksReady, false);
  assert.equal(result.keepReadyDuringRerun, true);
  const pending = { ...check, status: 'IN_PROGRESS', conclusion: null,
    checkSuite: { ...check.checkSuite, workflowRun: { databaseId: 11, workflow: { databaseId: 1 } } } };
  const previousRun = { id: 10, head_sha: head, workflow_id: 1, status: 'completed', conclusion: 'success', pull_requests: [{ number: 1 }] };
  result = await inspect({ checks: [pending, rabbit], runs: [previousRun] });
  assert.equal(result.keepReadyDuringRerun, true);
  result = await inspect({ checks: [pending, rabbit], runs: [{ ...previousRun, id: 11, run_attempt: 2, conclusion: null }], previousAttempt: previousRun });
  assert.equal(result.keepReadyDuringRerun, true);
  result = await inspect({ checks: [pending, rabbit], runs: [{ ...previousRun, id: 11, run_attempt: 2, conclusion: null }], previousAttempt: { ...previousRun, conclusion: 'failure' } });
  assert.equal(result.keepReadyDuringRerun, false);
  for (const changed of [{ head_sha: 'old' }, { workflow_id: 2 }, { pull_requests: [{ number: 2 }] }, { conclusion: 'failure' }]) {
    result = await inspect({ checks: [pending, rabbit], runs: [{ ...previousRun, ...changed }] });
    assert.equal(result.keepReadyDuringRerun, false);
  }
  result = await inspect({ checks: [check, { ...check, databaseId: 2, conclusion: 'FAILURE' }, rabbit] });
  assert.equal(result.keepReadyDuringRerun, false);
  delete process.env.AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT;
  result = await inspect({ checks: [{ ...check, conclusion: 'FAILURE' }], comments: [limited()] });
  assert.equal(result.rateLimited, true);
  assert.equal(result.checksReady, false);
  result = await inspect({ checks: [check, { ...rabbit, state: 'PENDING', createdAt: '2026-09-02T00:00:00Z' }],
    comments: [limited()] });
  assert.equal(result.codeRabbitReady, false);

  // Следующая страница может содержать обязательную проверку и результат кролика.
  result = await inspect({ pages: [[], [check, rabbit]] });
  assert.equal(result.checksReady, true);
  assert.equal(result.codeRabbitReady, true);
  await assert.rejects(inspect({ responseHead: 'new-head' }), /ПР изменился/);
  await assert.rejects(inspect({ fail: 'checks' }), /checks denied/);
  await assert.rejects(inspect({ fail: 'rules' }), /rules denied/);
  // Обязательный сценарий сверяется по источнику и текущему коммиту, а не по названию.
  const workflow = { path: '.github/workflows/required.yml', repository_id: 7 };
  const run = { id: 1, head_sha: head, repository: { id: 7 }, path: workflow.path + '@master', event: 'pull_request',
    status: 'completed', conclusion: 'success', pull_requests: [{ number: 1 }] };
  result = await inspect({ workflows: [workflow] });
  assert.equal(result.checksReady, false);
  result = await inspect({ workflows: [workflow], runs: [run] });
  assert.equal(result.checksReady, true);
  for (const changed of [{ head_sha: 'old' }, { repository: { id: 8 } },
    { path: '.github/workflows/spoof.yml@master' }, { status: 'in_progress' }, { event: 'push' },
    { conclusion: 'skipped' }, { conclusion: 'failure' }, { pull_requests: [{ number: 2 }] }]) {
    result = await inspect({ workflows: [workflow], runs: [{ ...run, ...changed }] });
    assert.equal(result.checksReady, false, JSON.stringify(changed));
  }
  result = await inspect({ workflows: [workflow], runs: [run, { ...run, id: 2, conclusion: 'failure' }] });
  assert.equal(result.checksReady, false);
  const referenced = { ...run, repository: { id: 8 }, path: '.github/workflows/caller.yml',
    referenced_workflows: [{ path: `example/repo/${workflow.path}@refs/heads/master`, sha: 'pinned', ref: 'refs/heads/master' }] };
  result = await inspect({ workflows: [{ ...workflow, sha: 'pinned', ref: 'refs/heads/master' }], runs: [referenced] });
  assert.equal(result.checksReady, false);
  result = await inspect({ workflows: [{ ...workflow, sha: 'pinned' }],
    runs: [{ ...run, repository: { id: 8 }, path: `example/repo/${workflow.path}@pinned` }] });
  assert.equal(result.checksReady, true);
  result = await inspect({ workflows: [{ ...workflow, sha: 'different' }], runs: [referenced] });
  assert.equal(result.checksReady, false);

  const { buildChecklist, syncChecklist } = require('../auto_draft/checklist.js');
  const state = { owner: 'example', repo: 'repo', number: 1, appSlug: 'autodraft',
    feedback: [{ text: 'Замечания reviewer', done: false }], readiness: await inspect() };
  const body = buildChecklist(state);
  assert.ok(body.includes('- [ ] Разобраться'));
  assert.ok(body.includes('- [x] Пройти обязательные'));
  assert.ok(body.includes('Summary'));
  assert.ok(body.includes('Пролистай страницу ПР вниз'));
  assert.ok(body.includes('<details>\n<summary>Как найти список ошибок тестов</summary>'));
  assert.ok(!body.includes('<details open'));
  assert.ok(body.includes('раскрой нужный шард'));
  assert.ok(body.includes('он может ошибаться'));
  assert.ok(!body.includes('Ручные правки сообщения'));
  const absentBody = buildChecklist({ ...state, readiness: { ...state.readiness, codeRabbitAbsent: true } });
  assert.ok(absentBody.includes('- [x] ~~Дождаться CodeRabbit~~'));
  assert.ok(!absentBody.includes('искусственный интеллект'));
  assert.ok(buildChecklist({ ...state, manualDraft: true }).includes('- [ ] Подтвердить'));
  assert.ok(buildChecklist({ ...state, manualOverride: true }).includes('аварийный'));
  const hostile = buildChecklist({ ...state, feedback: [{ done: false, text: '@someone #456\n- [x] <script> [click](https://example.org)' }] });
  assert.ok(!hostile.includes('@someone'));
  assert.ok(!hostile.includes('#456'));
  assert.ok(!hostile.includes('<script>'));
  assert.ok(!hostile.includes('\n- [x] <'));
  let comments = [];
  const calls = [];
  const github = { async paginate() { return comments; }, rest: { issues: {
    listComments() {}, async createComment(params) { calls.push(['create', params]); },
    async updateComment(params) { calls.push(['update', params]); },
    async deleteComment(params) { calls.push(['delete', params]); },
  } } };
  await syncChecklist({ github, ...state });
  assert.equal(calls.pop()[0], 'create');
  const owned = { id: 7, user: { type: 'Bot', login: 'autodraft[bot]' }, body };
  comments = [owned];
  await syncChecklist({ github, ...state });
  assert.equal(calls.length, 0);
  comments = [{ ...owned, body: 'Всё готово, маркер тоже удалён' }];
  await syncChecklist({ github, ...state });
  assert.deepEqual(calls.pop(), ['update', { owner: 'example', repo: 'repo', comment_id: 7, body }]);
  comments = [{ ...owned, user: { type: 'User', login: 'contributor' } }];
  await syncChecklist({ github, ...state });
  assert.equal(calls.pop()[0], 'create');
  comments = [owned, { ...owned, id: 8 }];
  await syncChecklist({ github, ...state });
  assert.equal(calls.pop()[0], 'delete');

  const { buildReport, publishReport } = require('../auto_draft/report.js');
  const green = buildReport({ number: 1, readiness: await inspect(), action: 'ready' });
  assert.equal(green.title, 'Автодрафт: всё готово');
  assert.equal(green.conclusion, 'success');
  const blocked = buildReport({ number: 1, feedback: state.feedback, readiness: await inspect(), action: 'draft' });
  assert.equal(blocked.title, 'Автодрафт: нужны исправления');
  assert.equal(blocked.conclusion, 'neutral');
  const failed = buildReport({ number: 1, readiness: await inspect({ checks: [{ ...check, conclusion: 'FAILURE' }, rabbit] }), action: 'draft' });
  assert.equal(failed.title, 'Автодрафт: ошибки проверок');
  assert.ok(failed.summary.includes('Tests: ошибка'));
  assert.equal(buildReport({ number: 1, error: new Error('API denied') }).conclusion, 'failure');
  assert.equal(buildReport({ number: 1, manualDraft: true }).title, 'Автодрафт: ручной черновик');
  assert.equal(buildReport({ number: 1, manualOverride: true }).title, 'Автодрафт: ручной режим');
  let statusCheck;
  let writes = 0;
  const checkGithub = { async paginate() { return statusCheck ? [statusCheck] : []; }, rest: { checks: {
    listForRef() {},
    async create(params) { writes++; statusCheck = { ...params, id: 9, app: { slug: 'github-actions' } }; },
    async update(params) { writes++; statusCheck = { ...statusCheck, ...params }; },
  } } };
  const reportParams = { github: checkGithub, core: { info() {} }, owner: 'example', repo: 'repo', number: 1, head, report: green };
  await publishReport(reportParams);
  assert.equal(writes, 1);
  await publishReport(reportParams);
  assert.equal(writes, 1, 'Неизменившийся результат не требует записи');
  await publishReport({ ...reportParams, report: blocked });
  assert.equal(writes, 2);
  assert.equal(statusCheck.id, 9, 'Причина меняется в той же проверке');
  assert.equal(statusCheck.name, blocked.title);
  await publishReport({ ...reportParams, existing: statusCheck, report: blocked,
    github: { ...checkGithub, paginate() { throw new Error('Не нужен повторный запрос'); } } });
  assert.equal(writes, 2);
  console.log('Readiness scenarios passed');
})().catch(error => { console.error(error); process.exitCode = 1; });
