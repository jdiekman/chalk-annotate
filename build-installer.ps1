#Requires -Version 7
# Builds the Chalk installer + portable exe into dist/.
# Used identically on a dev machine and in CI (.github/workflows/release.yml).
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$csprojPath = Join-Path $repoRoot 'src/ScreenAnnotationTool/ScreenAnnotationTool.csproj'

$version = ([xml](Get-Content $csprojPath)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> element found in $csprojPath" }

dotnet publish $csprojPath -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$publishDir = Join-Path $repoRoot 'src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish'

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw 'Inno Setup 6 (ISCC.exe) not found. Install it with: winget install JRSoftware.InnoSetup'
}

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $repoRoot 'installer/chalk.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

Copy-Item (Join-Path $publishDir 'Chalk.exe') (Join-Path $repoRoot "dist/Chalk-$version-portable.exe") -Force
Write-Host "Built dist/ChalkSetup-$version.exe and dist/Chalk-$version-portable.exe"
