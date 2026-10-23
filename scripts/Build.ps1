param(
    [string]$GameRoot = $env:DTMAPI_GAME_DIR,
    [string]$SdkRoot = (Join-Path $PSScriptRoot '..\.tools\dtmapi-sdk')
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$authorPath = Join-Path $repoRoot 'mod\dtmapi.author.json'
if (Test-Path -LiteralPath $authorPath) {
    $authorConfig = Get-Content -LiteralPath $authorPath -Raw | ConvertFrom-Json
} else {
    $authorConfig = Get-Content -LiteralPath (Join-Path $repoRoot 'mod\dtmapi.author.example.json') -Raw | ConvertFrom-Json
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = $authorConfig.nativeReferences.gameRoot }
if ([string]::IsNullOrWhiteSpace($GameRoot)) { throw 'Pass -GameRoot or set DTMAPI_GAME_DIR to your Doloc Town installation.' }
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'DolocTown_Data\Managed\Assembly-CSharp.dll'))) { throw 'GameRoot does not contain the required Doloc Town assembly.' }
if (!(Test-Path -LiteralPath $authorPath) -or $authorConfig.nativeReferences.gameRoot -ne $GameRoot) {
    $authorConfig.nativeReferences.gameRoot = $GameRoot
    $authorConfig | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $authorPath -Encoding utf8
}
$sdkPath = (Resolve-Path -LiteralPath $SdkRoot).Path
. (Join-Path $sdkPath 'Enter-DtmApiEnvironment.ps1')
$cli = Join-Path $sdkPath 'dtmapi-author.exe'
$reports = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
function Invoke-Report([string]$Name, [string[]]$Arguments) {
    $raw = & $cli @Arguments --json
    $exitCode = $LASTEXITCODE
    $raw | Set-Content -LiteralPath (Join-Path $reports "$Name.json") -Encoding utf8
    if ($exitCode -ne 0) { throw "$Name failed. See artifacts/$Name.json" }
    return (($raw -join "`n") | ConvertFrom-Json)
}
Push-Location $repoRoot
try {
    & (Join-Path $sdkPath 'toolchain\dotnet\dotnet.exe') run --project tests\MovementTests
    if ($LASTEXITCODE -ne 0) { throw 'Movement tests failed.' }
    $null = Invoke-Report 'restore-report' @('restore', 'mod', '--offline', 'true')
    $manifest = Get-Content -LiteralPath 'mod\manifest.json' -Raw | ConvertFrom-Json
    $package = Join-Path $repoRoot "mod\dist\$($manifest.UniqueID)-$($manifest.Version).zip"
    if (Test-Path -LiteralPath $package) {
        $history = Join-Path $reports 'history'
        New-Item -ItemType Directory -Path $history -Force | Out-Null
        $destination = Join-Path $history ("$($manifest.UniqueID)-$($manifest.Version)-" + [guid]::NewGuid().ToString('N') + '.zip')
        if (!$package.StartsWith($repoRoot + '\') -or !$destination.StartsWith($repoRoot + '\')) { throw 'Path outside workspace.' }
        Move-Item -LiteralPath $package -Destination $destination
    }
    $pack = Invoke-Report 'pack-report' @('pack', 'mod', '--offline', 'true', '--symbols', 'true', '--game-root', $GameRoot)
    if (!$pack.success) { throw 'SDK pack report did not confirm success.' }
    $doctor = Invoke-Report 'doctor-package' @('doctor', $pack.outputPath)
    if ($doctor.errorCount -gt 0) { throw 'Package Doctor reported errors.' }
    Write-Host "Package: $($pack.outputPath)"
    Write-Host "Doctor: $($doctor.errorCount) errors, $($doctor.warningCount) warnings. In-game testing remains separate."
} finally { Pop-Location }
