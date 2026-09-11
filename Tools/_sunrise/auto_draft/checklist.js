const marker = '<!-- auto-draft-checklist:v1 -->';

// Нейтрализуем разметку и упоминания из названий проверок и имён ревьюверов.
function plain(text) {
  return String(text).replace(/@/g, '＠').replace(/#/g, '＃').replace(/[\r\n]+/g, ' ')
    .replace(/([\\`*_{}\[\]<>()!|])/g, '\\$1');
}

function buildChecklist({ owner, repo, number, feedback, readiness, manualDraft, manualOverride }) {
  const prUrl = `https://github.com/${owner}/${repo}/pull/${number}`;
  const checkbox = (done, text) => `- [${done ? 'x' : ' '}] ${text}`;
  const lines = [marker, '### Готовим изменения к ревью', '',
    'Привет! Здесь видно, что осталось сделать перед проверкой человеком. Пролистай страницу ПР вниз до блока проверок: там видны тесты и их результаты. Галочки в этом списке обновляются автоматически.', '',
    checkbox(feedback.every(item => item.done), `Разобраться с замечаниями. Исправь код и закрой решённые обсуждения во вкладке [Files changed — изменённые файлы](${prUrl}/files). Если не согласен, обсуди это с ревьювером.`),
    ...feedback.map(item => `  ${checkbox(item.done, plain(item.text))}`),
    ...(readiness.codeRabbitAbsent ? [checkbox(true, `~~Дождаться CodeRabbit~~ — бот не появился за ${readiness.codeRabbitWaitMinutes} минут, поэтому ожидание пропущено.`)] : [checkbox(readiness.codeRabbitReady, readiness.rateLimited
      ? 'Проверка CodeRabbit: достигнут лимит запросов, поэтому сейчас разрешено продолжить без нового ревью.'
      : 'Дождаться CodeRabbit: он должен проверить последнюю версию кода. Если бот не появится за 10 минут после создания ПР, этот пункт будет пропущен.'),
    '  CodeRabbit — искусственный интеллект: он может ошибаться и предлагать бессмысленные исправления. Сам проверь, действительно ли найден баг. Исправляй настоящие ошибки, а с неверным замечанием объясни своё несогласие в обсуждении.']),
    checkbox(readiness.checksReady, `Пройти обязательные проверки внизу страницы ПР. Нажми на нужную проверку, чтобы посмотреть результат. Также доступна вкладка [Checks — проверки](${prUrl}/checks). Жёлтая проверка ещё выполняется; красная завершилась с ошибкой.`),
    ...(readiness.checkItems || []).map(item => `  ${checkbox(item.done, plain(item.name))}`),
    '', '<details>', '<summary>Как найти список ошибок тестов</summary>', '',
    '1. Пролистай ПР вниз до блока проверок и нажми на упавший тест. Можно также открыть его во вкладке Checks.',
    '2. На странице задания нажми Summary — сводка запуска.',
    '3. В сводке раскрой нужный шард — группу тестов, например Integration Tests (shard 0). Там будет список ошибок.',
    '4. Исправь причину и отправь изменения в этот ПР. Если сводка не содержит ошибок, открой журнал упавшего шага задания.',
    '', '</details>',
  ];
  if (readiness.error)
    lines.push('', 'Не удалось получить часть данных GitHub. Бот попробует ещё раз; пока неподтверждённые пункты не отмечены.');
  if (manualDraft)
    lines.push('', checkbox(false, 'Подтвердить готовность своего черновика: когда закончишь работу, нажми Ready for review — готово к ревью. Бот сохраняет черновики, созданные вручную.'));
  lines.push('', manualOverride
    ? 'Сейчас действует ручной аварийный переход в готовое состояние. Старые замечания не вернут этот ПР в черновик; новое требование исправлений снова включит автоматику.'
    : manualDraft ? 'Остальные пункты помогут подготовить изменения перед ручным открытием.'
      : 'Когда все пункты выполнены, бот сам переведёт ПР из черновика в готовое состояние. Обновление иногда занимает несколько минут.');
  return lines.join('\n');
}

async function syncChecklist({ github, owner, repo, number, appSlug, comments, ...state }) {
  if (!appSlug)
    throw new Error('Не задано имя приложения автодрафта.');
  const body = buildChecklist({ owner, repo, number, ...state });
  comments ||= await github.paginate(github.rest.issues.listComments, {
    owner, repo, issue_number: number, per_page: 100,
  });
  // Приложение выделено для автодрафта: узнаём его комментарий даже после удаления маркера человеком.
  const owned = comments.filter(comment => comment.user?.type === 'Bot' &&
    comment.user.login === `${appSlug}[bot]`).sort((a, b) => a.id - b.id);
  const existing = owned[0];
  if (!existing)
    await github.rest.issues.createComment({ owner, repo, issue_number: number, body });
  else if (existing.body !== body)
    await github.rest.issues.updateComment({ owner, repo, comment_id: existing.id, body });
  for (const duplicate of owned.slice(1))
    await github.rest.issues.deleteComment({ owner, repo, comment_id: duplicate.id });
}

module.exports = { buildChecklist, syncChecklist, plain };
