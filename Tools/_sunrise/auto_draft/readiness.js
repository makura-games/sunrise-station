module.exports = async ({ github, commentsGithub = github, owner, repo, pullRequest, rulesCache,
  reportAppSlug = 'github-actions', now = Date.now() }) => {
  const checks = [];
  let cursor = null;
  let createdAt;
  let hasRabbitReview = false;
  do {
    const result = await github.graphql(
      `query Readiness($owner: String!, $repo: String!, $number: Int!, $cursor: String) {
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
      }`,
      { owner, repo, number: pullRequest.number, cursor },
    );
    const current = result.repository.pullRequest;
    if (!current || current.headRefOid !== pullRequest.headRefOid || current.baseRefName !== pullRequest.baseRefName)
      throw new Error('ПР изменился во время чтения проверок; требуется повторная синхронизация.');
    createdAt = current.createdAt;
    hasRabbitReview = current.reviews?.nodes.some(review => review.author?.__typename === 'Bot') || false;
    const connection = current.commits.nodes[0]?.commit.statusCheckRollup?.contexts;
    checks.push(...(connection?.nodes || []));
    cursor = connection?.pageInfo.hasNextPage ? connection.pageInfo.endCursor : null;
  } while (cursor);

  // Повторный запуск заменяет старый результат, но одноимённые задания разных workflow независимы.
  const checkKey = check => check.__typename === 'CheckRun'
    ? `run:${check.checkSuite.app?.databaseId}:${check.checkSuite.workflowRun?.workflow.databaseId}:${check.name}`
    : `status:${check.context}`;
  const latest = new Map();
  for (const check of checks) {
    const isRun = check.__typename === 'CheckRun';
    const key = checkKey(check);
    const previous = latest.get(key);
    const order = isRun ? check.databaseId : Date.parse(check.createdAt);
    const previousOrder = previous?.__typename === 'CheckRun'
      ? previous.databaseId : Date.parse(previous?.createdAt);
    if (!previous || order > previousOrder)
      latest.set(key, check);
  }
  const currentChecks = [...latest.values()];
  const isRabbit = check => check.__typename === 'StatusContext'
    ? check.context === 'CodeRabbit' && check.creator?.__typename === 'Bot' && check.creator.login === 'coderabbitai'
    : check.checkSuite.app?.slug === 'coderabbitai';
  const rabbitChecks = currentChecks.filter(isRabbit);
  const succeeded = check => check.__typename === 'StatusContext'
    ? check.state === 'SUCCESS'
    : check.status === 'COMPLETED' && ['SUCCESS', 'NEUTRAL', 'SKIPPED'].includes(check.conclusion);
  const reviewed = rabbitChecks.some(check => succeeded(check) &&
    /^Review completed\b/i.test(check.description || check.title || ''));

  const comments = await commentsGithub.paginate(commentsGithub.rest.issues.listComments, {
    owner, repo, issue_number: pullRequest.number, per_page: 100,
  });
  const rabbitComments = comments.filter(comment => comment.user?.type === 'Bot' && comment.user.login === 'coderabbitai[bot]');
  const codeRabbitWaitMinutes = 10;
  const codeRabbitAbsent = rabbitChecks.length === 0 && !hasRabbitReview && rabbitComments.length === 0 &&
    now - Date.parse(createdAt) >= codeRabbitWaitMinutes * 60_000;
  // Формулировки и оформление уведомлений меняются; источник обязательно должен быть настоящим ботом.
  const mentionsLimit = text => /rate[\s_-]*limit(?:ed|ing)?|(?:review|usage|request)\s+(?:limit|quota)(?:\s+(?:has\s+been|is))?\s+(?:reached|exceeded|exhausted)|(?:used|exhausted)\s+(?:all\s+)?(?:\w+\s+){0,4}(?:reviews|quota)|(?:лимит|квота)\s+(?:[\p{L}]+\s+){0,3}(?:исчерпан|превышен|достигнут)|(?:исчерпан|превышен|достигнут)[а-я]*\s+(?:лимит|квота)/iu.test(text || '');
  let rateLimited = false;
  if (!reviewed && process.env.AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT !== 'false') {
    rateLimited = rabbitChecks.some(check => mentionsLimit(check.description || check.title)) || rabbitComments.some(comment => {
      if (!mentionsLimit(comment.body))
        return false;
      // Если кролик уже начал новый проход, старое сообщение о лимите не заменяет его результат.
      return !rabbitChecks.some(check => check.state === 'PENDING' &&
        Date.parse(check.createdAt) > Date.parse(comment.updated_at));
    });
  }
  const codeRabbitReady = codeRabbitAbsent || reviewed || rateLimited;

  if (!rulesCache.has(pullRequest.baseRefName)) {
    const rules = await github.paginate('GET /repos/{owner}/{repo}/rules/branches/{branch}', {
      owner, repo, branch: pullRequest.baseRefName, per_page: 100,
    });
    rulesCache.set(pullRequest.baseRefName, rules);
  }
  // Сводка защиты ветки доступна с Contents: read; поле GraphQL требует прав администратора.
  const classicKey = `classic:${pullRequest.baseRefName}`;
  if (!rulesCache.has(classicKey)) {
    const { data: branch } = await github.rest.repos.getBranch({ owner, repo, branch: pullRequest.baseRefName });
    const required = branch.protection?.required_status_checks;
    rulesCache.set(classicKey, branch.protection?.enabled === false || required?.enforcement_level === 'off'
      ? [] : (required?.checks?.length ? required.checks : required?.contexts?.map(context => ({ context })) || []));
  }
  const requirements = rulesCache.get(classicKey).map(check => ({ context: check.context, integration_id: check.app_id }));
  const workflows = [];
  for (const rule of rulesCache.get(pullRequest.baseRefName)) {
    if (rule.type === 'required_status_checks')
      requirements.push(...rule.parameters.required_status_checks);
    if (rule.type === 'workflows')
      workflows.push(...rule.parameters.workflows);
  }
  const requiredChecks = currentChecks.filter(check => check.isRequired);
  const missingChecks = requirements.filter(requirement => !requiredChecks.some(check =>
    (check.name || check.context) === requirement.context &&
    (requirement.integration_id == null || requirement.integration_id === -1 ||
      check.__typename === 'StatusContext' || check.checkSuite.app?.databaseId === requirement.integration_id)));
  const checkItems = [
    ...missingChecks.map(check => ({ name: check.context, done: false, result: 'EXPECTED' })),
    ...requiredChecks.map(check => ({ name: check.name || check.context,
      done: isRabbit(check) ? codeRabbitReady : succeeded(check), result: check.conclusion || check.status || check.state })),
  ];
  let runs;
  const loadRuns = async () => runs ||= await github.paginate(github.rest.actions.listWorkflowRunsForRepo, {
    owner, repo, head_sha: pullRequest.headRefOid, per_page: 100,
  });
  // После Ready некоторые сценарии запускаются заново на том же коммите.
  // Их ожидание не должно создавать цикл Ready → Draft → Ready; новый провал по-прежнему блокирует.
  let keepReadyDuringRerun = missingChecks.length === 0 && workflows.length === 0;
  for (const check of requiredChecks.filter(check => !succeeded(check))) {
    if (check.__typename !== 'CheckRun' || check.status === 'COMPLETED') {
      keepReadyDuringRerun = false;
      break;
    }
    if (checks.some(previous => checkKey(previous) === checkKey(check) && succeeded(previous)))
      continue;
    const currentRun = check.checkSuite.workflowRun;
    const history = currentRun?.databaseId ? await loadRuns() : [];
    if (history.some(run =>
      run.head_sha === pullRequest.headRefOid && run.id < currentRun.databaseId &&
      run.workflow_id === currentRun.workflow.databaseId && run.status === 'completed' && run.conclusion === 'success' &&
      run.pull_requests?.some(pr => pr.number === pullRequest.number)))
      continue;
    // Кнопка Re-run сохраняет ID запуска, но увеличивает номер попытки.
    const rerun = history.find(run => run.id === currentRun?.databaseId && run.run_attempt > 1);
    if (rerun) {
      const attemptKey = `attempt:${rerun.id}:${rerun.run_attempt - 1}`;
      if (!rulesCache.has(attemptKey)) {
        const { data: attempt } = await github.rest.actions.getWorkflowRunAttempt({
          owner, repo, run_id: rerun.id, attempt_number: rerun.run_attempt - 1,
        });
        rulesCache.set(attemptKey, attempt);
      }
      const attempt = rulesCache.get(attemptKey);
      if (attempt.head_sha === pullRequest.headRefOid && attempt.status === 'completed' && attempt.conclusion === 'success')
        continue;
    }
    keepReadyDuringRerun = false;
    break;
  }
  if (workflows.length > 0) {
    await loadRuns();
    for (const workflow of workflows) {
      const sourceKey = `workflow-repository:${workflow.repository_id}`;
      if (!rulesCache.has(sourceKey)) {
        const { data: source } = await github.request('GET /repositories/{repository_id}', {
          repository_id: workflow.repository_id,
        });
        rulesCache.set(sourceKey, source);
      }
      const source = rulesCache.get(sourceKey);
      const sourcePath = `${source.full_name}/${workflow.path}`;
      const version = workflow.sha || workflow.ref || `refs/heads/${source.default_branch}`;
      const versions = new Set([version, version.replace(/^refs\/(heads|tags)\//, '')]);
      const matching = runs.filter(run => {
        if (run.head_sha !== pullRequest.headRefOid ||
            !['pull_request', 'pull_request_target', 'merge_group'].includes(run.event) ||
            !run.pull_requests?.some(pr => pr.number === pullRequest.number))
          return false;
        // Вызванная зависимость из referenced_workflows не доказывает запуск обязательного сценария.
        const [path, ref] = (run.path || '').split('@');
        return versions.has(ref) && (path === sourcePath ||
          run.repository?.id === workflow.repository_id && path === workflow.path);
      }).sort((a, b) => b.id - a.id);
      const latestRun = matching[0];
      checkItems.push({ name: `Сценарий ${workflow.path}`,
        done: latestRun?.status === 'completed' && latestRun.conclusion === 'success',
        result: (latestRun?.conclusion || latestRun?.status || 'EXPECTED').toUpperCase() });
    }
  }
  return {
    comments,
    reportCheck: checks.filter(check => check.checkSuite?.app?.slug === reportAppSlug &&
      (check.externalId === `auto-draft:${pullRequest.number}` || check.externalId?.startsWith(`auto-draft:${pullRequest.number}:`)))
      .sort((a, b) => b.databaseId - a.databaseId).map(check => ({ id: check.databaseId, name: check.name,
        external_id: check.externalId, conclusion: check.conclusion?.toLowerCase() }))[0] || null,
    checksReady: checkItems.every(item => item.done),
    codeRabbitReady,
    codeRabbitAbsent,
    codeRabbitWaitMinutes,
    keepReadyDuringRerun,
    rateLimited,
    pendingChecks: checkItems.filter(item => !item.done).map(item => item.name),
    checkItems,
  };
};
