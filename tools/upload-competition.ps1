<#
.SYNOPSIS
  Uploads the release APK to the "Competition" release channel of your Meta Horizon app.
.DESCRIPTION
  Before running:
    1. Create the app in the Developer Dashboard and a Private release channel named exactly "Competition".
    2. Note the App ID (Dashboard > your app > Development > API).
    3. Generate a platform utility token (Dashboard > Account > Account settings > "Get Oculus platform utility
       token" > Generate token). Or use the app secret from the same API page with -UseAppSecret.
  The token or secret is read with a hidden prompt. It is never printed or written to disk.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/upload-competition.ps1 -AppId 1234567890123456
#>
param(
  [Parameter(Mandatory = $true)][string]$AppId,
  [string]$Channel = "Competition",
  [string]$Apk = "builds/android/CursivisSpatial.apk",
  [ValidateSet("TEENS_AND_ADULTS", "MIXED_AGES", "CHILDREN")][string]$AgeGroup = "TEENS_AND_ADULTS",
  [switch]$UseAppSecret,
  [string]$Notes = "Cursivis Spatial 1.0.1 for the Meta Horizon Start Developer Competition 2026."
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
if ($AppId -notmatch '^\d{6,20}$') { throw "The App ID is the long number on Dashboard > your app > Development > API." }
if (-not (Test-Path $Apk)) { throw "APK not found: $Apk" }
$tool = Join-Path $env:APPDATA "metavr\tools\platform-utils\ovr-platform-util.exe"
if (-not (Test-Path $tool)) {
  $c = Get-Command ovr-platform-util -ErrorAction SilentlyContinue
  if ($c) { $tool = $c.Source } else { throw "ovr-platform-util not found. Install it with: metavr tools install platform-utils" }
}
$what = if ($UseAppSecret) { "app secret" } else { "platform utility token" }
$sec = Read-Host -AsSecureString "Paste your $what (input is hidden), then press Enter"
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
$code = 1
try {
  $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  $authFlag = if ($UseAppSecret) { "--app-secret" } else { "--token" }
  Write-Output "Uploading $Apk to channel '$Channel' of app $AppId (age group $AgeGroup)..."
  & $tool upload-quest-build --age-group $AgeGroup --app-id $AppId $authFlag $plain --apk (Resolve-Path $Apk).Path --channel $Channel --notes $Notes
  $code = $LASTEXITCODE
} finally {
  [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
  Remove-Variable plain -ErrorAction SilentlyContinue
}
if ($code -eq 0) {
  Write-Output ""
  Write-Output "Uploaded. Next, in the Dashboard: Distribution > Release Channels > $Channel > Channel Settings >"
  Write-Output "tick 'Grant access to users by URL', choose 'Automatic approval', Save Changes, then copy the URL."
} else {
  Write-Output "Upload failed (exit code $code). Copy the error text above."
}
exit $code
