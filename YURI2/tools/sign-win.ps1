# Signs YURI.exe with an Authenticode certificate.
#
#   powershell -File tools\sign-win.ps1 -Exe .\YURI.exe -Pfx .\yuri.pfx -Password '...'
#   powershell -File tools\sign-win.ps1 -Exe .\YURI.exe -Thumbprint A1B2...   # token / cert store
#
# WHY THIS EXISTS
# SmartScreen's "Unknown publisher" is not a metadata problem and cannot be
# fixed in the build: it is what Windows says about an executable carrying no
# Authenticode signature at all. Nothing about the icon, the version resource or
# the file name changes it. Only a certificate does.
#
# Reputation then accrues to the CERTIFICATE rather than to the file, which is
# the part that matters: unsigned, every new build is a brand new unknown file
# and starts from zero. Signed, each release inherits what the last one earned.
param(
  [Parameter(Mandatory=$true)][string]$Exe,
  [string]$Pfx,
  [string]$Password,
  [string]$Thumbprint,
  [string]$TimestampUrl = "http://timestamp.digicert.com"
)
$ErrorActionPreference = "Stop"
$st = Get-ChildItem -Recurse "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -match 'x64' } | Select-Object -Last 1
if (-not $st) { throw "signtool.exe not found - install the Windows SDK (Signing Tools)" }

# /fd and /td sha256: SHA-1 signatures are refused by current Windows.
# /tr timestamps the signature, so it stays valid after the certificate expires.
$common = @("sign", "/fd", "sha256", "/td", "sha256", "/tr", $TimestampUrl, "/v")
if ($Thumbprint) { $args = $common + @("/sha1", $Thumbprint, $Exe) }
elseif ($Pfx)    { $args = $common + @("/f", $Pfx) + $(if ($Password) { @("/p", $Password) } else { @() }) + @($Exe) }
else             { throw "give -Pfx (with -Password) or -Thumbprint" }

& $st.FullName @args
if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
& $st.FullName verify /pa /v $Exe
