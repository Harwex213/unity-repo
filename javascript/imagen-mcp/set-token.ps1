# Saves REMOTE_IMAGEGEN_TOKEN as a user environment variable on Windows.
# Usage:
#   .\set-token.ps1 <token>
#   .\set-token.ps1            # asks for the token
param(
    [string]$Token
)

$ErrorActionPreference = 'Stop'

if (-not $Token) {
    $secure = Read-Host 'REMOTE_IMAGEGEN_TOKEN' -AsSecureString
    $Token = [System.Net.NetworkCredential]::new('', $secure).Password
}

$Token = $Token.Trim()
if (-not $Token) {
    Write-Error 'Token is empty.'
    exit 1
}

setx REMOTE_IMAGEGEN_TOKEN "$Token" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "setx failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# setx affects only new processes, so set the variable for this session too.
$env:REMOTE_IMAGEGEN_TOKEN = $Token

Write-Host 'REMOTE_IMAGEGEN_TOKEN is saved. Restart the terminal and Claude Code to pick it up.'
