#!/usr/bin/env bash

set -euo pipefail

report_status() {
    local command="$1"
    local icon="$2"
    local message="$3"

    echo "::${command}::${message}"
    if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
        printf -- "- %s %s\n" "$icon" "$message" >> "$GITHUB_STEP_SUMMARY"
    fi
}

git config user.name "Sunrise-Bot"
git config user.email "sunrise.project.top@gmail.com"

commit_message_file="$(mktemp)"
trap 'rm -f -- "$commit_message_file"' EXIT
export CHANGELOG_COMMIT_MESSAGE_FILE="$commit_message_file"

if [[ -z "${CHANGELOG_FILE:-}" ]]; then
    report_status error "❌" "Переменная CHANGELOG_FILE не задана."
    exit 1
fi
changelog_directory="$(dirname -- "$CHANGELOG_FILE")"
changelog_files=("$CHANGELOG_FILE")
IFS=',' read -ra extra_categories <<< "${CHANGELOG_EXTRA_CATEGORIES:-}"
for category in "${extra_categories[@]}"; do
    category="${category//[[:space:]]/}"
    if [[ -z "$category" ]]; then
        continue
    fi
    if [[ "$category" == /* || "$category" == *".."* || ! "$category" =~ ^[A-Za-z]+$ ]]; then
        report_status error "❌" "Недопустимое имя категории чейнжлога: $category"
        exit 1
    fi
    changelog_files+=("$changelog_directory/$category.yml")
done

for attempt in {1..5}; do
    : > "$commit_message_file"
    git fetch --no-tags origin master
    git reset --hard origin/master
    git clean -fd -- Resources/Changelog/Parts

    arguments=(--event-path "$GITHUB_EVENT_PATH" --target-branch master)
    if [[ "${GITHUB_EVENT_NAME:-}" == "workflow_dispatch" ]]; then
        if [[ -z "${MANUAL_CHANGELOG:-}" ]]; then
            report_status error "❌" "Для ручного запуска необходимо заполнить поле чейнжлога."
            exit 1
        fi
        arguments+=(--manual-changelog)
    fi

    python Tools/_sunrise/changelog/changelog_actions.py "${arguments[@]}"
    git add -- "${changelog_files[@]}"
    git add -A -- Resources/Changelog/Parts

    if git diff --cached --quiet; then
        report_status notice "✅" "Чейнджлог уже актуален: публикация не требуется."
        exit 0
    fi

    if [[ ! -s "$commit_message_file" ]]; then
        report_status error "❌" "Не сформировано сообщение коммита чейнжлога."
        exit 1
    fi
    git commit -F "$commit_message_file"
    if git push origin HEAD:master; then
        report_status notice "✅" "Чейнджлог успешно опубликован в master."
        exit 0
    fi

    report_status warning "⚠️" "Отправка не удалась, повторяем поверх свежего master (${attempt}/5)."
    sleep $((attempt * 2))
done

report_status error "❌" "Не удалось отправить чейнджлог после пяти попыток."
exit 1
