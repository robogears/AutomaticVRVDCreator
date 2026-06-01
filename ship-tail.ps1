#requires -version 5
<#
.SYNOPSIS
  Post-tag ship tail: wait for CI, verify the release body (GitHub actions often leave it
  empty), and leave the release as a DRAFT for manual publish. See the global ship.md.
.EXAMPLE
  .\ship-tail.ps1 v0.1.1
#>
param([Parameter(Mandatory)][string]$Tag)

$ErrorActionPreference = 'Stop'

$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = "$env:LOCALAPPDATA\Microsoft\WinGet\Links\gh.exe" }
if (-not (Test-Path $gh)) { throw "gh CLI not found. Install with: winget install GitHub.cli" }

# 1. Wait for the tag's CI run to finish.
Write-Host "Waiting for the latest 'Release' workflow run..."
$run = & $gh run list --workflow release.yml --limit 1 --json databaseId --jq ".[0].databaseId"
if ($run) {
    & $gh run watch $run --exit-status
    if ($LASTEXITCODE -ne 0) { Write-Error "CI failed for $Tag (run $run)."; exit 1 }
} else {
    Write-Warning "No 'Release' workflow run found yet. Re-run after CI starts."
}

# 2. Body verification. Set it from RELEASE_NOTES.md if empty/short.
$len = & $gh release view $Tag --json body --jq "(.body | length)"
if ([int]$len -lt 50) {
    Write-Host "Release body short ($len chars). Setting from RELEASE_NOTES.md."
    & $gh release edit $Tag --notes-file RELEASE_NOTES.md
    $len = & $gh release view $Tag --json body --jq "(.body | length)"
    Write-Host "Release body now $len chars."
} else {
    Write-Host "Release body OK ($len chars)."
}

# 3. Summary; leave as draft for manual publish.
& $gh release view $Tag --json url,isDraft,assets --jq "{url, isDraft, assets: [.assets[].name]}"
Write-Host ""
Write-Host "Review the draft + artifacts, then publish manually:"
Write-Host "    gh release edit $Tag --draft=false"
