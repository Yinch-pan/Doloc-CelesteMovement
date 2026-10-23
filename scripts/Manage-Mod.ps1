param(
    [Parameter(Mandatory)][ValidateSet('Deploy', 'Update', 'Status', 'Withdraw')][string]$Action,
    [string]$GameRoot = $env:DTMAPI_GAME_DIR,
    [string]$SdkRoot = (Join-Path $PSScriptRoot '..\.tools\dtmapi-sdk'),
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\mod\dist\Yinch.CelesteMovement-0.2.10.zip')
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $authorPath = Join-Path $PSScriptRoot '..\mod\dtmapi.author.json'
    if (Test-Path -LiteralPath $authorPath) {
        $GameRoot = (Get-Content -LiteralPath $authorPath -Raw | ConvertFrom-Json).nativeReferences.gameRoot
    }
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) { throw 'Pass -GameRoot or run Build.ps1 with your game path first.' }
$cli = Join-Path (Resolve-Path -LiteralPath $SdkRoot).Path 'dtmapi-author.exe'
$commands = @{ Deploy = 'deploy'; Update = 'update'; Status = 'deployment-status'; Withdraw = 'withdraw' }
$target = 'Yinch.CelesteMovement'
if ($Action -in @('Deploy', 'Update')) { $target = (Resolve-Path -LiteralPath $PackagePath).Path }
$raw = & $cli $commands[$Action] $target --game-root $GameRoot --json
$exitCode = $LASTEXITCODE
$reports = Join-Path $PSScriptRoot '..\artifacts'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$raw | Set-Content -LiteralPath (Join-Path $reports "$($Action.ToLowerInvariant())-report.json") -Encoding utf8
$raw
if ($exitCode -ne 0) { throw "SDK $Action failed. The report above describes the cause." }
