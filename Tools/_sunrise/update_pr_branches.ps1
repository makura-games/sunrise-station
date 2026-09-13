<#
Updates open pull request branches from the selected base branch.

Default double-click flow:
1. Run Tools/update_pr_branches.cmd.
2. Choose D for dry run, or A for apply.
3. Apply mode also requires typing UPDATE.

Command-line examples:
  powershell -File Tools/update_pr_branches.ps1 -DryRun
  powershell -File Tools/update_pr_branches.ps1 -Apply
  powershell -File Tools/update_pr_branches.ps1 -Repo makura-games/sunrise-station -BaseBranch master -DryRun
#>

param(
    [string] $Repo = "",
    [string] $BaseBranch = "master",
    [int] $Limit = 1000,
    [switch] $Apply,
    [switch] $DryRun,
    [switch] $NonInteractive,
    [int] $DelayMilliseconds = 500,
    [string] $LogPath = ""
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function Get-RepoFromOrigin {
    $origin = (& git remote get-url origin 2>$null)

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($origin)) {
        throw "Cannot determine repository: git remote 'origin' is missing."
    }

    $origin = $origin.Trim()

    if ($origin -match "^https://github\.com/([^/]+)/(.+?)(?:\.git)?$") {
        return "$($Matches[1])/$($Matches[2])"
    }

    if ($origin -match "^git@github\.com:([^/]+)/(.+?)(?:\.git)?$") {
        return "$($Matches[1])/$($Matches[2])"
    }

    throw "Cannot parse GitHub repository from origin URL: $origin"
}

function Initialize-Log {
    param([string] $RequestedPath)

    if ([string]::IsNullOrWhiteSpace($RequestedPath)) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        return Join-Path ([System.IO.Path]::GetTempPath()) "sunrise-update-pr-branches-$stamp.log"
    }

    $parent = Split-Path -Parent $RequestedPath
    if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    return $RequestedPath
}

function Write-Log {
    param([string] $Message = "")

    Write-Host $Message
    Add-Content -LiteralPath $script:ResolvedLogPath -Value $Message -Encoding UTF8
}

function Invoke-Gh {
    param([string[]] $Arguments)

    $output = & gh @Arguments 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = @($output)
    }
}

if ($Apply -and $DryRun) {
    throw "Use either -Apply or -DryRun, not both."
}

if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = Get-RepoFromOrigin
}

$script:ResolvedLogPath = Initialize-Log -RequestedPath $LogPath
New-Item -ItemType File -Path $script:ResolvedLogPath -Force | Out-Null

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI is not installed or is not available in PATH."
}

$auth = Invoke-Gh -Arguments @("auth", "status")
if ($auth.ExitCode -ne 0) {
    $auth.Output | ForEach-Object { Write-Host $_ }
    throw "GitHub CLI is not authenticated. Run 'gh auth login' first."
}

if (-not $Apply -and -not $DryRun) {
    if ($NonInteractive) {
        $DryRun = $true
    }
    else {
        Write-Host ""
        Write-Host "Sunrise PR branch updater"
        Write-Host "Repository: $Repo"
        Write-Host "Base branch: $BaseBranch"
        Write-Host ""
        Write-Host "D - dry run only"
        Write-Host "A - apply updates"
        Write-Host "Q - quit"
        $choice = Read-Host "Select mode"

        switch -Regex ($choice) {
            "^[Aa]$" { $Apply = $true; break }
            "^[Dd]$" { $DryRun = $true; break }
            default { Write-Host "Cancelled."; exit 0 }
        }
    }
}

if ($Apply -and -not $NonInteractive) {
    Write-Host ""
    Write-Host "Apply mode will update PR branches on GitHub by running:"
    Write-Host "  gh pr update-branch <number> --repo $Repo"
    Write-Host ""
    $confirmation = Read-Host "Type UPDATE to continue"
    if ($confirmation -ne "UPDATE") {
        Write-Host "Cancelled."
        exit 0
    }
}

$mode = if ($Apply) { "apply" } else { "dry-run" }
$started = Get-Date

Write-Log "Repository: $Repo"
Write-Log "Base branch: $BaseBranch"
Write-Log "Mode: $mode"
Write-Log "Started: $($started.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Log "Log: $script:ResolvedLogPath"
Write-Log ""

$list = Invoke-Gh -Arguments @(
    "pr", "list",
    "--repo", $Repo,
    "--state", "open",
    "--base", $BaseBranch,
    "--limit", "$Limit",
    "--json", "number,headRefName,isCrossRepository,maintainerCanModify"
)

if ($list.ExitCode -ne 0) {
    $list.Output | ForEach-Object { Write-Log "  $_" }
    throw "Failed to list pull requests."
}

$prs = @($list.Output -join "`n" | ConvertFrom-Json)

if ($prs.Count -eq 0) {
    Write-Log "No open PRs found for base branch '$BaseBranch'."
    exit 0
}

$dryRunCandidates = New-Object System.Collections.Generic.List[string]
$updated = New-Object System.Collections.Generic.List[string]
$upToDate = New-Object System.Collections.Generic.List[string]
$skipped = New-Object System.Collections.Generic.List[string]
$conflicts = New-Object System.Collections.Generic.List[string]
$errors = New-Object System.Collections.Generic.List[string]

Write-Log "PRs found: $($prs.Count)"
Write-Log ""

foreach ($pr in $prs) {
    $id = "#$($pr.number)"
    Write-Log "[$id] $($pr.headRefName)"

    if ($pr.isCrossRepository -and -not $pr.maintainerCanModify) {
        Write-Log "  SKIP: cross-repository PR without maintainer write permission."
        $skipped.Add($id) | Out-Null
        continue
    }

    if ($DryRun) {
        Write-Log "  DRY-RUN: gh pr update-branch $($pr.number) --repo $Repo"
        $dryRunCandidates.Add($id) | Out-Null
        continue
    }

    $result = Invoke-Gh -Arguments @(
        "pr", "update-branch", "$($pr.number)",
        "--repo", $Repo
    )

    $combinedOutput = ($result.Output -join "`n")

    if ($result.ExitCode -eq 0) {
        if ($combinedOutput -match "already up-to-date") {
            Write-Log "  OK: already up-to-date."
            $upToDate.Add($id) | Out-Null
        }
        else {
            Write-Log "  OK: branch updated."
            $updated.Add($id) | Out-Null
        }
    }
    elseif ($combinedOutput -match "(?i)conflict") {
        Write-Log "  CONFLICT: GitHub cannot update this branch automatically."
        $result.Output | ForEach-Object { Write-Log "    $_" }
        $conflicts.Add($id) | Out-Null
    }
    else {
        Write-Log "  ERROR: gh exited with code $($result.ExitCode)."
        $result.Output | ForEach-Object { Write-Log "    $_" }
        $errors.Add($id) | Out-Null
    }

    if ($DelayMilliseconds -gt 0) {
        Start-Sleep -Milliseconds $DelayMilliseconds
    }
}

$finished = Get-Date
Write-Log ""
Write-Log "======= SUMMARY ======="
Write-Log "Finished: $($finished.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Log "Duration seconds: $([int]($finished - $started).TotalSeconds)"

if ($DryRun) {
    Write-Log "Would update: $($dryRunCandidates.Count) $($dryRunCandidates -join ' ')"
}
else {
    Write-Log "Updated: $($updated.Count) $($updated -join ' ')"
    Write-Log "Already up-to-date: $($upToDate.Count) $($upToDate -join ' ')"
}

Write-Log "Skipped: $($skipped.Count) $($skipped -join ' ')"
Write-Log "Conflicts: $($conflicts.Count) $($conflicts -join ' ')"
Write-Log "Errors: $($errors.Count) $($errors -join ' ')"
Write-Log "Log: $script:ResolvedLogPath"

if ($errors.Count -gt 0) {
    exit 1
}

exit 0
