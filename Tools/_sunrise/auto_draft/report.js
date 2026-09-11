const { plain } = require('./checklist.js');
const { createHash } = require('node:crypto');

function buildReport({ number, feedback = [], readiness = {}, manualDraft, manualOverride, action, error, skipped }) {
  const results = { EXPECTED: 'ещё не запущена', QUEUED: 'в очереди', PENDING: 'ожидается', IN_PROGRESS: 'выполняется',
    SUCCESS: 'успешно', FAILURE: 'ошибка', ERROR: 'ошибка', CANCELLED: 'отменена', TIMED_OUT: 'истекло время',
    SKIPPED: 'пропущена по условию', NEUTRAL: 'нейтральный результат', ACTION_REQUIRED: 'нужно действие человека' };
  const unresolved = feedback.filter(item => !item.done);
  const failedItem = item => !item.done && ['FAILURE', 'ERROR', 'CANCELLED', 'TIMED_OUT', 'ACTION_REQUIRED', 'STARTUP_FAILURE'].includes(item.result);
  const failed = (readiness.checkItems || []).some(failedItem);
  let reason = 'всё готово';
  if (error) reason = 'ошибка синхронизации';
  else if (skipped) reason = 'повторная проверка позже';
  else if (manualOverride) reason = 'ручной режим';
  else if (manualDraft) reason = 'ручной черновик';
  else if (unresolved.length) reason = 'нужны исправления';
  else if (failed) reason = 'ошибки проверок';
  else if (!readiness.checksReady) reason = readiness.keepReadyDuringRerun ? 'повторный запуск тестов' : 'ждём проверки';
  else if (!readiness.codeRabbitReady) reason = 'ждём CodeRabbit';
  const conclusion = error ? 'failure' : reason === 'всё готово' ? 'success' : 'neutral';
  const title = `Автодрафт: ${reason}`;
  const lines = [`## ${error ? '❌' : conclusion === 'success' ? '✅' : 'ℹ️'} ПР ${number}: ${reason}`, ''];
  if (error) {
    lines.push(`Не удалось завершить синхронизацию: ${plain(error.message || error)}`, '',
      'Проверь сообщение об ошибке и последний этап в журнале. При отказе в доступе проверь права приложения и подтверждение его установки; при недоступности GitHub повтори запуск. Успешная готовность не подтверждена.');
  } else if (skipped) {
    lines.push(plain(skipped));
  } else {
    lines.push(`- ${unresolved.length ? '⏳ Остались требования исправлений' : '✅ Открытых требований исправлений нет'}.`,
      ...unresolved.map(item => `  - ${plain(item.text)}`),
      `- ${readiness.checksReady ? '✅ Обязательные проверки пройдены' : '⏳ Обязательные проверки не завершены успешно'}.`,
      ...(readiness.checkItems || []).map(item => `  - ${item.done ? '✅' : failedItem(item) ? '❌' : '⏳'} ${plain(item.name)}: ${plain(results[item.result] || item.result || (item.done ? 'успешно' : 'ожидается'))}.`),
      `- ${readiness.codeRabbitAbsent ? 'ℹ️ CodeRabbit не появился за 10 минут: ожидание пропущено' :
        readiness.rateLimited ? '✅ CodeRabbit сообщил о лимите: исключение разрешено' :
          readiness.codeRabbitReady ? '✅ CodeRabbit закончил ревью' : '⏳ Ожидается CodeRabbit'}.`, '');
    if (manualDraft) lines.push('Ручной черновик сохранён: автор сам подтверждает готовность.');
    else if (manualOverride) lines.push('Сохранён ручной аварийный переход. Новое требование исправлений снова включит автоматику.');
    else if (!readiness.checksReady && readiness.keepReadyDuringRerun && action === 'keep')
      lines.push('ПР оставлен готовым: повторяется ранее успешная проверка того же коммита. Новый провал снова заблокирует его.');
    else lines.push({ draft: 'ПР переведён в черновик.', ready: 'ПР открыт для ревью.',
      cleanup: 'Устаревшая служебная метка снята.', keep: 'Состояние ПР не изменено: оно соответствует условиям выше.' }[action]);
  }
  return { title, conclusion, summary: lines.filter(line => line !== undefined).join('\n') };
}

async function publishReport({ github, core, owner, repo, number, head, report, runId, existing, checkAppSlug = 'github-actions' }) {
  core.info(report.summary);
  if (core.summary)
    await core.summary.addRaw(report.summary + '\n\n').write();
  if (!head) return;
  const prefix = `auto-draft:${number}`;
  const externalId = `${prefix}:${createHash('sha256').update(report.summary).digest('hex')}`;
  // В обычном пути проверка уже прочитана вместе с готовностью. Дополнительный запрос нужен лишь при сбое чтения.
  if (existing === undefined) {
    const checks = await github.paginate(github.rest.checks.listForRef, { owner, repo, ref: head, filter: 'all', per_page: 100 });
    existing = checks.filter(check => (check.external_id === prefix || check.external_id?.startsWith(prefix + ':')) && check.app?.slug === checkAppSlug)
      .sort((a, b) => b.id - a.id)[0];
  }
  const detailsUrl = runId ? `https://github.com/${owner}/${repo}/actions/runs/${runId}` : `https://github.com/${owner}/${repo}/pull/${number}`;
  const parameters = { owner, repo, name: report.title, status: 'completed', conclusion: report.conclusion,
    external_id: externalId, details_url: detailsUrl, output: { title: report.title, summary: report.summary } };
  // Имя меняется вместе с причиной, идентификатор сохраняется: новые строки проверки не накапливаются.
  if (existing) {
    if (existing.name !== report.title || existing.conclusion !== report.conclusion || existing.external_id !== externalId)
      await github.rest.checks.update({ ...parameters, check_run_id: existing.id });
  } else {
    await github.rest.checks.create({ ...parameters, head_sha: head });
  }
}

module.exports = { buildReport, publishReport };
