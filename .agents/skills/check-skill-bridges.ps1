param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$sourceRoot = Join-Path $RepoRoot ".agents/skills"
$claudeBridgeRoot = Join-Path $RepoRoot ".claude/skills"
$cursorBridgeRoot = Join-Path $RepoRoot ".cursor/skills"
$githubBridgeRoot = Join-Path $RepoRoot ".github/skills"

function Get-SkillData {
    param([string]$SkillMdPath)

    $raw = Get-Content $SkillMdPath -Raw
    $fmMatch = [regex]::Match($raw, "(?ms)^---\r?\n(.*?)\r?\n---")
    if (-not $fmMatch.Success) {
        throw "No YAML frontmatter in $SkillMdPath"
    }

    $frontmatter = $fmMatch.Groups[1].Value
    $body = $raw.Substring($fmMatch.Index + $fmMatch.Length).Trim()
    $name = [regex]::Match($frontmatter, "(?m)^name:\s*(.+)$").Groups[1].Value.Trim()
    $description = [regex]::Match($frontmatter, "(?m)^description:\s*(.+)$").Groups[1].Value.Trim()
    $sourceSkill = [regex]::Match(
        $frontmatter,
        "(?m)^\s*source_skill:\s*`"?([^`"\r\n]+)`"?\s*$"
    ).Groups[1].Value.Trim()

    return @{
        raw = $raw
        body = $body
        name = $name
        description = $description
        source_skill = $sourceSkill
    }
}

if (-not (Test-Path $sourceRoot)) {
    throw "Source skills path not found: $sourceRoot"
}
if (-not (Test-Path $claudeBridgeRoot)) {
    throw "Claude bridge skills path not found: $claudeBridgeRoot"
}
if (-not (Test-Path $cursorBridgeRoot)) {
    throw "Cursor bridge skills path not found: $cursorBridgeRoot"
}
if (-not (Test-Path $githubBridgeRoot)) {
    throw "GitHub Copilot bridge skills path not found: $githubBridgeRoot"
}

$errors = New-Object System.Collections.Generic.List[string]

$sourceSkills = Get-ChildItem $sourceRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
    Sort-Object Name
$claudeBridgeSkills = Get-ChildItem $claudeBridgeRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
    Sort-Object Name
$cursorBridgeSkills = Get-ChildItem $cursorBridgeRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
    Sort-Object Name
$githubBridgeSkills = Get-ChildItem $githubBridgeRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "SKILL.md") } |
    Sort-Object Name

$sourceNames = @($sourceSkills.Name)
$claudeBridgeNames = @($claudeBridgeSkills.Name)
$cursorBridgeNames = @($cursorBridgeSkills.Name)
$githubBridgeNames = @($githubBridgeSkills.Name)

foreach ($source in $sourceSkills) {
    $name = $source.Name
    $sourceSkillMd = Join-Path $source.FullName "SKILL.md"
    $claudeBridgeSkillMd = Join-Path $claudeBridgeRoot "$name/SKILL.md"
    $cursorBridgeSkillMd = Join-Path $cursorBridgeRoot "$name/SKILL.md"
    $githubBridgeSkillMd = Join-Path $githubBridgeRoot "$name/SKILL.md"

    if (-not (Test-Path $claudeBridgeSkillMd)) {
        $errors.Add("Missing Claude bridge SKILL.md for '$name'.")
    }
    if (-not (Test-Path $cursorBridgeSkillMd)) {
        $errors.Add("Missing Cursor bridge SKILL.md for '$name'.")
    }
    if (-not (Test-Path $githubBridgeSkillMd)) {
        $errors.Add("Missing GitHub Copilot bridge SKILL.md for '$name'.")
    }
    if ((-not (Test-Path $claudeBridgeSkillMd)) -or (-not (Test-Path $cursorBridgeSkillMd)) -or (-not (Test-Path $githubBridgeSkillMd))) {
        continue
    }

    $sourceData = Get-SkillData -SkillMdPath $sourceSkillMd
    $claudeData = Get-SkillData -SkillMdPath $claudeBridgeSkillMd
    $cursorData = Get-SkillData -SkillMdPath $cursorBridgeSkillMd
    $githubData = Get-SkillData -SkillMdPath $githubBridgeSkillMd

    $expectedSourceSkill = "../../../.agents/skills/$name/SKILL.md"

    if ($sourceData.name -ne $name) {
        $errors.Add("Source skill name mismatch for '$name': '$($sourceData.name)'")
    }

    if ($claudeData.name -ne $name) {
        $errors.Add("Claude bridge name mismatch for '$name': '$($claudeData.name)'")
    }
    if ($claudeData.description -ne $sourceData.description) {
        $errors.Add("Claude bridge description mismatch for '$name'.")
    }
    if ($claudeData.body -notmatch [regex]::Escape($expectedSourceSkill)) {
        $errors.Add("Claude bridge reference mismatch for '$name'.")
    }

    if ($cursorData.name -ne $name) {
        $errors.Add("Cursor bridge name mismatch for '$name': '$($cursorData.name)'")
    }
    if ($cursorData.description -ne $sourceData.description) {
        $errors.Add("Cursor bridge description mismatch for '$name'.")
    }
    if ($cursorData.body -notmatch [regex]::Escape($expectedSourceSkill)) {
        $errors.Add("Cursor bridge reference mismatch for '$name'.")
    }

    if ($githubData.name -ne $name) {
        $errors.Add("GitHub Copilot bridge name mismatch for '$name': '$($githubData.name)'")
    }
    if ($githubData.description -ne $sourceData.description) {
        $errors.Add("GitHub Copilot bridge description mismatch for '$name'.")
    }
    if ($githubData.source_skill -ne $expectedSourceSkill) {
        $errors.Add(
            "GitHub Copilot bridge source_skill mismatch for '$name': '$($githubData.source_skill)'"
        )
    }
    if ($githubData.body -notmatch [regex]::Escape($expectedSourceSkill)) {
        $errors.Add("GitHub Copilot bridge reference mismatch for '$name'.")
    }
}

foreach ($bridgeName in $claudeBridgeNames) {
    if ($sourceNames -notcontains $bridgeName) {
        $errors.Add("Claude bridge exists without source skill: '$bridgeName'.")
    }
}

foreach ($bridgeName in $cursorBridgeNames) {
    if ($sourceNames -notcontains $bridgeName) {
        $errors.Add("Cursor bridge exists without source skill: '$bridgeName'.")
    }
}

foreach ($bridgeName in $githubBridgeNames) {
    if ($sourceNames -notcontains $bridgeName) {
        $errors.Add("GitHub Copilot bridge exists without source skill: '$bridgeName'.")
    }
}

foreach ($sourceName in $sourceNames) {
    if ($claudeBridgeNames -notcontains $sourceName) {
        $errors.Add("Source skill missing in Claude bridge tree: '$sourceName'.")
    }
    if ($cursorBridgeNames -notcontains $sourceName) {
        $errors.Add("Source skill missing in Cursor bridge tree: '$sourceName'.")
    }
    if ($githubBridgeNames -notcontains $sourceName) {
        $errors.Add("Source skill missing in GitHub Copilot bridge tree: '$sourceName'.")
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Bridge check failed with $($errors.Count) issue(s):" -ForegroundColor Red
    foreach ($errorItem in $errors) {
        Write-Host "- $errorItem"
    }
    exit 1
}

Write-Host "Bridge check passed:"
Write-Host "- source skills: $($sourceSkills.Count)"
Write-Host "- claude bridges: $($claudeBridgeSkills.Count)"
Write-Host "- cursor bridges: $($cursorBridgeSkills.Count)"
Write-Host "- github copilot bridges: $($githubBridgeSkills.Count)"
exit 0
