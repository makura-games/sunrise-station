module.exports = async ({ github, context, core }) => {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const markerLabel = process.env.AUTO_DRAFT_LABEL;

  // AUTO_DRAFT_POLICY_START
  function decideDraftState({
    isDraft,
    hasMarker,
    latestBlockingAt,
    latestApprovalAt,
    latestReadyAt,
    allBlockingThreadsResolved,
  }) {
    const hasBlockingReview = latestBlockingAt !== null;
    const approvalOverrides =
      hasBlockingReview &&
      latestApprovalAt !== null &&
      latestApprovalAt > latestBlockingAt;
    const shouldBeReady =
      !hasBlockingReview ||
      approvalOverrides ||
      allBlockingThreadsResolved;

    if (isDraft)
      return hasMarker && shouldBeReady ? "ready" : "keep";

    const blockingReviewIsNew =
      hasBlockingReview &&
      (latestReadyAt === null || latestBlockingAt > latestReadyAt);

    if (!shouldBeReady && blockingReviewIsNew)
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
    const result = await github.graphql(
      `query($owner: String!, $repo: String!, $number: Int!) {
        repository(owner: $owner, name: $repo) {
          pullRequest(number: $number) {
            id
            number
            state
            isDraft
            labels(first: 100) {
              nodes {
                name
              }
            }
            latestOpinionatedReviews(first: 100) {
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
            reviewThreads(first: 100) {
              nodes {
                isResolved
                comments(first: 1) {
                  nodes {
                    pullRequestReview {
                      id
                    }
                  }
                }
              }
            }
            timelineItems(last: 1, itemTypes: [READY_FOR_REVIEW_EVENT]) {
              nodes {
                ... on ReadyForReviewEvent {
                  createdAt
                }
              }
            }
          }
        }
      }`,
      { owner, repo, number },
    );

    return result.repository.pullRequest;
  }

  async function addMarker(number) {
    await github.rest.issues.addLabels({
      owner,
      repo,
      issue_number: number,
      labels: [markerLabel],
    });
  }

  async function removeMarker(number) {
    try {
      await github.rest.issues.removeLabel({
        owner,
        repo,
        issue_number: number,
        name: markerLabel,
      });
    } catch (error) {
      if (error.status !== 404)
        throw error;
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
    const pullRequest = await loadPullRequest(number);
    if (!pullRequest || pullRequest.state !== "OPEN") {
      core.info(`#${number}: ПР закрыт или не найден, пропускаю.`);
      return;
    }

    const reviews = pullRequest.latestOpinionatedReviews.nodes
      .filter(review => review.authorCanPushToRepository);
    const blockingReviews = reviews
      .filter(review => review.state === "CHANGES_REQUESTED");
    const approvals = reviews
      .filter(review => review.state === "APPROVED");
    const blockingReviewIds = new Set(blockingReviews.map(review => review.id));
    const threadsByReview = new Map();

    for (const thread of pullRequest.reviewThreads.nodes) {
      const reviewId = thread.comments.nodes[0]?.pullRequestReview?.id;
      if (!reviewId || !blockingReviewIds.has(reviewId))
        continue;

      const reviewThreads = threadsByReview.get(reviewId) || [];
      reviewThreads.push(thread);
      threadsByReview.set(reviewId, reviewThreads);
    }

    const allBlockingThreadsResolved =
      blockingReviews.length > 0 &&
      blockingReviews.every(review => {
        const reviewThreads = threadsByReview.get(review.id) || [];
        return reviewThreads.length > 0 && reviewThreads.every(thread => thread.isResolved);
      });
    const latestBlockingAt = latestTimestamp(blockingReviews);
    const latestApprovalAt = latestTimestamp(approvals);
    const latestReadyEvent = pullRequest.timelineItems.nodes[0];
    const latestReadyAt = latestReadyEvent
      ? Date.parse(latestReadyEvent.createdAt)
      : null;
    const hasMarker = pullRequest.labels.nodes
      .some(label => label.name === markerLabel);
    const action = decideDraftState({
      isDraft: pullRequest.isDraft,
      hasMarker,
      latestBlockingAt,
      latestApprovalAt,
      latestReadyAt,
      allBlockingThreadsResolved,
    });

    core.info(
      `#${number}: action=${action}, draft=${pullRequest.isDraft}, ` +
      `blocking=${blockingReviews.length}, approvals=${approvals.length}, ` +
      `threadsResolved=${allBlockingThreadsResolved}.`,
    );

    if (action === "draft") {
      await addMarker(number);
      try {
        await convertToDraft(pullRequest.id);
      } catch (error) {
        if (!hasMarker)
          await removeMarker(number);
        throw error;
      }
      return;
    }

    if (action === "ready") {
      await removeMarker(number);
      try {
        await markReadyForReview(pullRequest.id);
      } catch (error) {
        await addMarker(number);
        throw error;
      }
      return;
    }

    if (action === "cleanup")
      await removeMarker(number);
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
    if (context.eventName === "workflow_run") {
      if (context.payload.workflow_run.conclusion !== "success") {
        core.info("Сигнальный workflow завершился неуспешно, синхронизация не требуется.");
        return [];
      }

      const numbers = context.payload.workflow_run.pull_requests
        .map(pullRequest => pullRequest.number)
        .filter(Number.isInteger);
      if (numbers.length === 0)
        core.warning("workflow_run не содержит связанного ПР; резервное расписание выполнит синхронизацию позже.");
      return [...new Set(numbers)];
    }

    if (context.eventName === "pull_request_target")
      return [context.payload.pull_request.number];

    if (context.eventName === "workflow_dispatch") {
      const requestedNumber = context.payload.inputs?.["pr-number"];
      if (requestedNumber === undefined || requestedNumber === "")
        return openPullRequestNumbers();

      const number = Number(requestedNumber);
      if (!Number.isInteger(number) || number <= 0)
        throw new Error(`Некорректный номер ПР: ${requestedNumber}`);
      return [number];
    }

    return openPullRequestNumbers();
  }

  // ponytail: первые 100 ревью и обсуждений покрывают обычный ПР; пагинацию добавим при реальной необходимости.
  const numbers = await targetPullRequestNumbers();
  const failures = [];
  for (const number of numbers) {
    try {
      await syncPullRequest(number);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      failures.push(`#${number}: ${message}`);
      core.error(`#${number}: ${message}`);
    }
  }

  if (failures.length > 0)
    throw new Error(`Не удалось синхронизировать ${failures.length} ПР:\n${failures.join("\n")}`);
};
