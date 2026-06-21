<#
.SYNOPSIS
    Loads credentials from a .env file and launches the Unity game server.

.DESCRIPTION
    The Unity server (ServerManager.LoadApiCredentials) reads API_USERNAME / API_PASSWORD
    from OS environment variables. This wrapper reads them from a .env file so they are
    never baked into the build and are kept out of git.

.PARAMETER ExtraArgs
    Any additional arguments are passed straight through to the Unity server.

.EXAMPLE
    .\run-server.ps1
    .\run-server.ps1 -logFile server.log

.NOTES
    Override paths with environment variables before running:
      $env:ENV_FILE   = path to the .env file   (default: <script dir>\.env)
      $env:SERVER_BIN = path to the server .exe  (default: auto-detected in <script dir>)
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$EnvFile = if ($env:ENV_FILE) { $env:ENV_FILE } else { Join-Path $ScriptDir '.env' }

# --- Load .env -------------------------------------------------------------
if (-not (Test-Path $EnvFile)) {
    Write-Error "env file not found: $EnvFile`n       Copy .env.example to .env and fill in the values."
    exit 1
}

foreach ($line in Get-Content -LiteralPath $EnvFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
    $trimmed = $trimmed -replace '^export\s+', ''      # allow optional "export " prefix
    $idx = $trimmed.IndexOf('=')
    if ($idx -lt 1) { continue }
    $key = $trimmed.Substring(0, $idx).Trim()
    $val = $trimmed.Substring($idx + 1).Trim()
    if (($val.StartsWith('"') -and $val.EndsWith('"')) -or
        ($val.StartsWith("'") -and $val.EndsWith("'"))) {
        $val = $val.Substring(1, $val.Length - 2)       # strip surrounding quotes
    }
    Set-Item -Path ("Env:" + $key) -Value $val
}

# --- Validate --------------------------------------------------------------
if ([string]::IsNullOrEmpty($env:API_USERNAME) -or [string]::IsNullOrEmpty($env:API_PASSWORD)) {
    Write-Error "API_USERNAME and/or API_PASSWORD are empty in $EnvFile"
    exit 1
}

# --- Resolve server binary -------------------------------------------------
$ServerBin = $env:SERVER_BIN
if ([string]::IsNullOrEmpty($ServerBin)) {
    $found = Get-ChildItem -LiteralPath $ScriptDir -Filter '*.exe' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch 'UnityCrashHandler' } | Select-Object -First 1
    if ($found) { $ServerBin = $found.FullName }
}
if ([string]::IsNullOrEmpty($ServerBin) -or -not (Test-Path $ServerBin)) {
    Write-Error "server binary not found. Set `$env:SERVER_BIN to the Unity server executable."
    exit 1
}

# --- Launch (headless) -----------------------------------------------------
Write-Host "Launching $ServerBin as user '$env:API_USERNAME'..."
$argList = @('-batchmode', '-nographics')
if ($ExtraArgs) { $argList += $ExtraArgs }
& $ServerBin @argList
exit $LASTEXITCODE
