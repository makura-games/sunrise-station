module.exports = async ({ github, readGithub = github, context, core, config = require('./config.json') }) => {
  const loadReadiness = require('./readiness.js');
  const { syncChecklist, plain } = require('./checklist.js');
  const { buildReport, publishReport } = require('./report.js');
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const label = config.label;
  const validName = name => typeof name === 'string' && name.trim().length > 0 && name.length <= 50 && !/[\r\n]/.test(name);
  if (!label || !validName(label.name) || !/^[a-f0-9]{6}$/i.test(label.color) ||
      typeof label.description !== 'string' || label.description.length > 100 ||
      !Array.isArray(label.previousNames) || !label.previousNames.every(validName))
    throw new Error('Некорректная метка в auto_draft/config.json: проверь name, шестизначный color, description и previousNames.');
  const markerLabel = label.name;
  const markerNames = [...new Set([markerLabel, ...label.previousNames])];
  const appSlug = process.env.AUTO_DRAFT_APP_SLUG;
  const reportAppSlug = readGithub === github ? appSlug : 'github-actions';
  const rulesCache = new Map();
  let currentPullRequest;
  let currentReportCheck;

  async function ensureLabel() {
    let existing;
    for (const name of markerNames) {
      try {
        existing = (await github.rest.issues.getLabel({ owner, repo, name })).data;
        break;
      } catch (error) {
        if (error.status !== 404) throw error;
      }
    }
    if (!existing) {
      await github.rest.issues.createLabel({ owner, repo, name: markerLabel, color: label.color, description: label.description });
    } else if (existing.name !== markerLabel || existing.color.toLowerCase() !== label.color.toLowerCase() || existing.description !== label.description) {
      await github.rest.issues.updateLabel({ owner, repo, name: existing.name, new_name: markerLabel,
        color: label.color, description: label.description });
    }
  }

  // AUTO_DRAFT_POLICY_START
  function decideDraftState({
    isDraft,
    hasMarker,
    latestBlockingAt,
    latestReadyAt,
    allBlockingThreadsResolved,
    checksReady,
    codeRabbitReady,
    keepReadyDuringRerun = false,
  }) {
    const hasBlockingReview = latestBlockingAt !== null;
    const shouldBeReady =
      (!hasBlockingReview || allBlockingThreadsResolved) && checksReady && codeRabbitReady;

    if (isDraft)
      return hasMarker && shouldBeReady ? "ready" : "keep";

    const manualOverride = latestReadyAt !== null &&
      (!hasBlockingReview || latestReadyAt >= latestBlockingAt);
    const waitingForRerun = (!hasBlockingReview || allBlockingThreadsResolved) &&
      codeRabbitReady && keepReadyDuringRerun;
    if (!shouldBeReady && !waitingForRerun && !manualOverride)
      return "draft";

    return hasMarker ? "cleanup" : "keep";
  }
  // AUTO_DRAFT_POLICY_END

  function latestTimestamp(items) {
    if (items.length === 0)
      return null;

    return Math.max(...items.map(item => Date.parse(item.submittedAt)));
  }

  async function loadPullRequest(number) {
    const connections = ["labels", "latestOpinionatedReviews", "reviewThreads"];
    let pullRequest;
    do {
      const variables = { owner, repo, number };
      for (const name of connections) {
        const page = pullRequest?.[name]?.pageInfo;
        variables[`${name}Cursor`] = page?.endCursor ?? null;
        variables[`load${name}`] = !page || page.hasNextPage;
      }

      const result = await github.graphql(
        `query($owner: String!, $repo: String!, $number: Int!,
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
              nodes {
                name
              }
            }
            latestOpinionatedReviews(first: 100, after: $latestOpinionatedReviewsCursor) @include(if: $loadlatestOpinionatedReviews) {
              pageInfo { hasNextPage endCursor }
              nodes {
                id
                state
                submittedAt
                authorCanPushToRepository
                author {
                  login
                }
              }
            }
            reviewThreads(first: 100, after: $reviewThreadsCursor) @include(if: $loadreviewThreads) {
              pageInfo { hasNextPage endCursor }
              nodes {
                isResolved
                comments(first: 1) {
                  nodes {
                    pullRequestReview {
                      id
                      state
                      author { login }
                    }
                  }
                }
              }
            }
            timelineItems(last: 1, itemTypes: [READY_FOR_REVIEW_EVENT]) {
              nodes {
                ... on ReadyForReviewEvent {
                  createdAt
                  actor { login }
                }
              }
            }
          }
        }
        }`,
        variables,
      );

      const next = result.repository.pullRequest;
      if (!next)
        return null;

      if (!pullRequest) {
        pullRequest = next;
      } else {
        for (const name of connections) {
          if (!next[name])
            continue;
          pullRequest[name].nodes.push(...next[name].nodes);
          pullRequest[name].pageInfo = next[name].pageInfo;
        }
      }
    } while (connections.some(name => pullRequest[name].pageInfo.hasNextPage));

    return pullRequest;
  }

  async function addMarker(number) {
    await github.rest.issues.addLabels({
      owner,
      repo,
      issue_number: number,
      labels: [markerLabel],
    });
  }

  async function removeMarker(number, names = markerNames) {
    for (const name of names) {
      try {
        await github.rest.issues.removeLabel({ owner, repo, issue_number: number, name });
      } catch (error) {
        if (error.status !== 404) throw error;
      }
    }
  }

  async function convertToDraft(id) {
    await github.graphql(
      `mutation($id: ID!) {
        convertPullRequestToDraft(input: { pullRequestId: $id }) {
          pullRequest {
            id
          }
        }
      }`,
      { id },
    );
  }

  async function markReadyForReview(id) {
    await github.graphql(
      `mutation($id: ID!) {
        markPullRequestReadyForReview(input: { pullRequestId: $id }) {
          pullRequest {
            id
          }
        }
      }`,
      { id },
    );
  }

  async function syncPullRequest(number) {
    core.info('Этап 1/4: читаю состояние ПР, решения ревьюверов и обсуждения.');
    const pullRequest = await loadPullRequest(number);
    currentPullRequest = pullRequest;
    if (!pullRequest || pullRequest.state !== "OPEN") {
      core.info(`#${number}: ПР закрыт или не найден, пропускаю.`);
      return buildReport({ number, skipped: 'ПР закрыт или не найден: синхронизация не нужна.' });
    }

    const reviews = pullRequest.latestOpinionatedReviews.nodes
      .filter(review => review.authorCanPushToRepository);
    const blockingReviews = reviews
      .filter(review => review.state === "CHANGES_REQUESTED");
    const approvals = reviews
      .filter(review => review.state === "APPROVED");
    const blockingReviewIds = new Set(blockingReviews.map(review => review.id));
    const blockingAuthors = new Set(blockingReviews.map(review => review.author?.login).filter(Boolean));
    const threadsByReview = new Map();
    const unresolvedByAuthor = new Set();
    let hasUnresolvedBlockingThreads = false;

    for (const thread of pullRequest.reviewThreads.nodes) {
      const review = thread.comments.nodes[0]?.pullRequestReview;
      if (!review || !(blockingReviewIds.has(review.id) ||
          review.state === 'CHANGES_REQUESTED' && blockingAuthors.has(review.author?.login)))
        continue;

      hasUnresolvedBlockingThreads ||= !thread.isResolved;
      if (!thread.isResolved && review.author?.login)
        unresolvedByAuthor.add(review.author.login);
      const reviewThreads = threadsByReview.get(review.id) || [];
      reviewThreads.push(thread);
      threadsByReview.set(review.id, reviewThreads);
    }

    const allBlockingThreadsResolved =
      blockingReviews.length > 0 &&
      !hasUnresolvedBlockingThreads &&
      blockingReviews.every(review => {
        const reviewThreads = threadsByReview.get(review.id) || [];
        return reviewThreads.length > 0 && reviewThreads.every(thread => thread.isResolved);
      });
    const latestBlockingAt = latestTimestamp(blockingReviews);
    const latestReadyEvent = pullRequest.timelineItems.nodes[0];
    // Собственный перевод в Ready не является ручным разрешением игнорировать старые замечания.
    const readyByApp = appSlug && latestReadyEvent?.actor?.login.replace(/\[bot\]$/, "") === appSlug;
    const latestReadyAt = latestReadyEvent && !readyByApp
      ? Date.parse(latestReadyEvent.createdAt)
      : null;
    const hasMarker = pullRequest.labels.nodes
      .some(label => markerNames.includes(label.name));
    const manualOverride = !pullRequest.isDraft && latestReadyAt !== null &&
      (latestBlockingAt === null || latestReadyAt >= latestBlockingAt);
    let readiness = { checksReady: false, codeRabbitReady: false, pendingChecks: [], checkItems: [] };
    let readinessError;
    core.info('Этап 2/4: проверяю обязательные тесты и ответ CodeRabbit.');
    try {
      readiness = await loadReadiness({ github: readGithub, commentsGithub: github, owner, repo, pullRequest, rulesCache, reportAppSlug });
      currentReportCheck = readiness.reportCheck;
    } catch (error) {
      readinessError = error;
      readiness.error = true;
      core.warning(`#${number}: не удалось получить готовность проверок: ${error.message}`);
    }
    const action = decideDraftState({
      isDraft: pullRequest.isDraft,
      hasMarker,
      latestBlockingAt,
      latestReadyAt,
      allBlockingThreadsResolved,
      ...readiness,
    });

    core.info(
      `#${number}: action=${action}, draft=${pullRequest.isDraft}, ` +
      `blocking=${blockingReviews.length}, approvals=${approvals.length}, ` +
      `threadsResolved=${allBlockingThreadsResolved}, checksReady=${readiness.checksReady}, ` +
      `codeRabbitReady=${readiness.codeRabbitReady}, rateLimited=${readiness.rateLimited || false}, ` +
      `pendingChecks=${readiness.pendingChecks.map(plain).join(', ')}.`,
    );

    const feedback = blockingReviews.map(review => {
      const threads = threadsByReview.get(review.id) || [];
      return {
        text: `Замечания ${review.author?.login || 'ревьювера'}${threads.length === 0 ? ': требуется новое решение ревьювера, обсуждений у этого требования нет' : ''}`,
        done: threads.length > 0 && threads.every(thread => thread.isResolved) &&
          !unresolvedByAuthor.has(review.author?.login),
      };
    });
    const reportState = { number, feedback, readiness, action,
      manualDraft: pullRequest.isDraft && !hasMarker, manualOverride };
    core.info('Этап 3/4: обновляю список задач в комментарии ПР.');
    await syncChecklist({ github, owner, repo, number, appSlug, feedback, readiness,
      manualDraft: pullRequest.isDraft && !hasMarker, manualOverride, comments: readiness.comments });
    // Сбой GitHub не доказывает неготовность ПР: обновляем пояснение, но не меняем черновик.
    if (readinessError)
      throw readinessError;

    if (action === 'draft' || action === 'ready') {
      const { data: current } = await github.rest.pulls.get({ owner, repo, pull_number: number });
      if (current.head.sha !== pullRequest.headRefOid || current.base.ref !== pullRequest.baseRefName ||
          current.draft !== pullRequest.isDraft || current.state !== 'open') {
        core.info(`#${number}: состояние ПР изменилось во время проверки, откладываю синхронизацию.`);
        return buildReport({ ...reportState, skipped: 'ПР изменился во время чтения. Устаревшее решение не применяется; следующий запуск перечитает данные.' });
      }
    }

    core.info(`Этап 4/4: ${buildReport(reportState).title}.`);
    const oldMarkers = pullRequest.labels.nodes.map(label => label.name)
      .filter(name => name !== markerLabel && markerNames.includes(name));
    if (action === 'keep' && pullRequest.isDraft && oldMarkers.length > 0) {
      if (!pullRequest.labels.nodes.some(label => label.name === markerLabel))
        await addMarker(number);
      await removeMarker(number, oldMarkers);
    }
    if (action === "draft") {
      if (!hasMarker)
        await addMarker(number);
      try {
        await convertToDraft(pullRequest.id);
      } catch (error) {
        if (!hasMarker)
          await removeMarker(number);
        throw error;
      }
      return buildReport(reportState);
    }

    if (action === "ready") {
      // Сохраняем метку до успешной смены статуса: повторный запуск сможет завершить очистку.
      await markReadyForReview(pullRequest.id);
      await removeMarker(number);
      return buildReport(reportState);
    }

    if (action === "cleanup")
      await removeMarker(number);
    return buildReport(reportState);
  }

  async function openPullRequestNumbers() {
    const pulls = await github.paginate(github.rest.pulls.list, {
      owner,
      repo,
      state: "open",
      per_page: 100,
    });
    return pulls.map(pull => pull.number);
  }

  async function targetPullRequestNumbers() {
    if (context.eventName === 'issue_comment') {
      const { issue, sender, comment, action } = context.payload;
      if (!issue.pull_request)
        return [];
      const rabbit = sender.type === 'Bot' && sender.login === 'coderabbitai[bot]';
      const checklistEdited = ['edited', 'deleted'].includes(action) && comment?.user?.type === 'Bot' &&
        comment.user.login === `${appSlug}[bot]` && sender.login !== comment.user.login;
      return rabbit || checklistEdited ? [issue.number] : [];
    }

    if (context.eventName === 'status')
      return associatedPullRequestNumbers(context.payload.sha);

    if (context.eventName === "workflow_run") {
      if (context.payload.workflow_run.name === 'PR: Automatic Draft Management - Review Events' &&
          context.payload.workflow_run.conclusion !== "success") {
        core.info("Сигнальный workflow завершился неуспешно, синхронизация не требуется.");
        return [];
      }

      const numbers = (context.payload.workflow_run.pull_requests || [])
        .map(pullRequest => pullRequest.number)
        .filter(number => Number.isSafeInteger(number) && number > 0);
      if (numbers.length === 0 && context.payload.workflow_run.head_sha) {
        // Для ПР из форков GitHub может не заполнить pull_requests; находим их по коммиту запуска.
        numbers.push(...await associatedPullRequestNumbers(context.payload.workflow_run.head_sha));
      }
      if (numbers.length === 0) {
        core.info("workflow_run не содержит связанного ПР; проверяю открытые ПР.");
        return openPullRequestNumbers();
      }
      return [...new Set(numbers)];
    }

    if (context.eventName === "pull_request_target")
      return [context.payload.pull_request.number];

    if (context.eventName === "workflow_dispatch") {
      const requestedNumber = context.payload.inputs?.["pr-number"];
      if (requestedNumber === undefined || requestedNumber === "")
        return openPullRequestNumbers();

      const number = Number(requestedNumber);
      if (!Number.isSafeInteger(number) || number <= 0)
        throw new Error(`Некорректный номер ПР: ${requestedNumber}`);
      return [number];
    }

    return openPullRequestNumbers();
  }

  async function associatedPullRequestNumbers(sha) {
    if (!sha)
      return [];
    try {
      const associated = await github.paginate(github.rest.repos.listPullRequestsAssociatedWithCommit, {
        owner, repo, commit_sha: sha, per_page: 100,
      });
      return [...new Set(associated.filter(pull => pull.state === 'open').map(pull => pull.number))];
    } catch (error) {
      if (error.status !== 404)
        throw error;
      core.info('Коммит события уже недоступен; связанных ПР не найдено.');
      return [];
    }
  }

  const numbers = await targetPullRequestNumbers();
  core.info(`Автодрафт запущен: событие ${context.eventName}; ПР для проверки: ${numbers.length}.`);
  if (numbers.length === 0) {
    core.info('Ничего не изменено: событие не требует пересчёта открытых ПР.');
    return;
  }
  await ensureLabel();
  const failures = [];
  for (const number of numbers) {
    currentPullRequest = null;
    currentReportCheck = undefined;
    core.startGroup?.(`ПР ${number}: проверка готовности`);
    try {
      const report = await syncPullRequest(number);
      await publishReport({ github: readGithub, core, owner, repo, number,
        head: currentPullRequest?.state === 'OPEN' ? currentPullRequest.headRefOid : null, report, runId: context.runId, existing: currentReportCheck, checkAppSlug: reportAppSlug });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      failures.push(`#${number}: ${message}`);
      core.error(`#${number}: ${message}`);
      const rateLimited = error.status === 429 || error.response?.headers?.['x-ratelimit-remaining'] === '0' ||
        error.status === 403 && /rate.?limit|secondary.*limit/i.test(message);
      try {
        await publishReport({ github: readGithub, core, owner, repo, number, head: rateLimited ? null : currentPullRequest?.headRefOid,
          report: buildReport({ number, error }), runId: context.runId, existing: currentReportCheck, checkAppSlug: reportAppSlug });
      } catch (reportError) {
        core.error(`Не удалось опубликовать результат ПР ${number}: ${reportError.message}`);
      }
      if (rateLimited) {
        core.warning('GitHub ограничил запросы. Обход остановлен без повторных запросов; оставшиеся ПР не получили подтверждение готовности. Повтори запуск после восстановления лимита.');
        break;
      }
    } finally {
      core.endGroup?.();
    }
  }

  if (failures.length > 0)
    throw new Error(`Не удалось синхронизировать ${failures.length} ПР:\n${failures.join("\n")}`);
  core.info(`✅ Синхронизация завершена успешно: обработано ${numbers.length} ПР. Причины решений записаны выше и в сводке запуска.`);
};
