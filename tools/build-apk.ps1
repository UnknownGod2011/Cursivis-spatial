<#
.SYNOPSIS
  Builds the release Quest APK (builds/android/CursivisSpatial.apk) against the public backend.
.DESCRIPTION
  - Backend URL: -Backend (default https://cursivis-spatial.vercel.app).
  - Client token: first entry of CURSIVIS_APP_TOKENS in backend/.env (rate-limit token, not a secret credential).
  - Release signing: if -Keystore exists, its password is read from the DPAPI-encrypted file next to it
    (keystore-pass.dpapi, decryptable only by the Windows user who created it) and handed to Unity through
    environment variables of the build process only. Nothing is written in plain text.
.EXAMPLE
  ./tools/build-apk.ps1 -VersionCode 1
#>
param(
  [string]$Backend = "https://cursivis-spatial.vercel.app",
  [int]$VersionCode = 1,
  [string]$Keystore = "C:\Users\Admin\cursivis-ref\keystore\cursivis-release.keystore",
  [string]$Name = "apk",
  [int]$TimeoutMinutes = 120
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$envFile = Join-Path $repo "backend/.env"
if (Test-Path $envFile) {
  $m = Select-String -Path $envFile -Pattern "^CURSIVIS_APP_TOKENS=(.+)$"
  if ($m) { $env:CURSIVIS_APP_TOKEN = $m.Matches[0].Groups[1].Value.Split(',')[0].Trim() }
}
$env:CURSIVIS_BACKEND_URL = $Backend
$env:CURSIVIS_BUILD_LABEL = "quest-release-$VersionCode"
$env:CURSIVIS_VERSION_CODE = "$VersionCode"
Remove-Item Env:CURSIVIS_DEVELOPMENT -ErrorAction SilentlyContinue
$dpapi = Join-Path (Split-Path -Parent $Keystore) "keystore-pass.dpapi"
if ((Test-Path $Keystore) -and (Test-Path $dpapi)) {
  $sec = Get-Content $dpapi | ConvertTo-SecureString
  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
  try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
  $env:CURSIVIS_KEYSTORE_PATH = $Keystore
  $env:CURSIVIS_KEYSTORE_PASS = $plain
  $env:CURSIVIS_KEY_ALIAS = "cursivis"
  $env:CURSIVIS_KEY_PASS = $plain
  Remove-Variable plain
  Write-Output "release signing: $(Split-Path -Leaf $Keystore)"
} else {
  Write-Output "no release keystore found: the APK will be debug-signed (sideload only)"
}
try {
  & (Join-Path $PSScriptRoot "unity-batch.ps1") -Method Cursivis.Spatial.EditorTools.CursivisBuild.BuildAndroid -Target Android -Name $Name -TimeoutMinutes $TimeoutMinutes
} finally {
  foreach ($v in "CURSIVIS_KEYSTORE_PASS", "CURSIVIS_KEY_PASS", "CURSIVIS_KEYSTORE_PATH", "CURSIVIS_APP_TOKEN") { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
}
