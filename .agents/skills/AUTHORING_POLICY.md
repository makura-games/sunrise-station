# Skill Authoring Policy

## Scope

This repository uses four skill trees:

- `.agents/skills` is the source of truth (used directly by Antigravity) and stores full skill content and resources.
- `.claude/skills` is the Claude Code compatibility layer.
- `.cursor/skills` is the Cursor compatibility layer.
- `.github/skills` is the GitHub Copilot compatibility layer.

## Required Rule

For every skill in `.agents/skills/<skill-name>`, keep matching bridge files:

- `.claude/skills/<skill-name>/SKILL.md`
- `.cursor/skills/<skill-name>/SKILL.md`
- `.github/skills/<skill-name>/SKILL.md`

When creating, renaming, deleting, or changing the `name`/`description` metadata of a skill in
`.agents/skills`, apply the matching bridge update in `.claude/skills`, `.cursor/skills`, and
`.github/skills` in the same pull request. Body-only edits to `.agents/skills/<skill-name>/SKILL.md`
do not require modifying bridge files if their metadata and source path already match.

## Bridge Contract

Each canonical source SKILL file in `.agents/skills/<skill-name>/SKILL.md` must contain:

- `name`: exact `<skill-name>` folder name in hyphen-case (`kebab-case`).
- `description`: canonical trigger and scope description.

Each Claude bridge SKILL file must contain:

- `name`: exact `<skill-name>` folder name in hyphen-case.
- `description`: synchronized copy of canonical description from `.agents/skills/<skill-name>/SKILL.md`.
- A reference in the markdown body to `../../../.agents/skills/<skill-name>/SKILL.md`.

Each Cursor bridge SKILL file must contain:

- `name`: exact `<skill-name>` folder name in hyphen-case.
- `description`: synchronized copy of canonical description from `.agents/skills/<skill-name>/SKILL.md`.
- A reference in the markdown body to `../../../.agents/skills/<skill-name>/SKILL.md`.

Each GitHub Copilot bridge SKILL file must contain:

- `name`: exact `<skill-name>` folder name in hyphen-case.
- `description`: synchronized copy of canonical description from `.agents/skills/<skill-name>/SKILL.md`.
- `metadata.source_skill`: `../../../.agents/skills/<skill-name>/SKILL.md`.
- A reference in the markdown body to `../../../.agents/skills/<skill-name>/SKILL.md`.

## PR Checklist Gate

A PR is incomplete if any skill exists in `.agents/skills` without matching bridges in
`.claude/skills`, `.cursor/skills`, and `.github/skills`.

Run this check before pushing:

`pwsh ./.agents/skills/check-skill-bridges.ps1`
