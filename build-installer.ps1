#Requires -Version 7
# Builds the Chalk installer + portable exe into dist/.
# Used identically on a dev machine and in CI (.github/workflows/release.yml).
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$csprojPath = Join-Path $repoRoot 'src/ScreenAnnotationTool/ScreenAnnotationTool.csproj'

$version = ([xml](Get-Content $csprojPath)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> element found in $csprojPath" }

$publishArguments = @(
    'publish', $csprojPath,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained',
    '-p:PublishSingleFile=true'
)
if ($NoRestore) { $publishArguments += '--no-restore' }

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$publishDir = Join-Path $repoRoot 'src/ScreenAnnotationTool/bin/Release/net8.0-windows/win-x64/publish'
$publishedExe = Join-Path $publishDir 'Chalk.exe'

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
}
if (-not $iscc) {
    throw 'Inno Setup 6 (ISCC.exe) not found. Install it with: winget install JRSoftware.InnoSetup'
}

$signTool = $null
$certificatePath = $null

function Sign-Artifact([string]$Path) {
    if (-not $script:certificatePath) { return }

    & $script:signTool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com `
        /f $script:certificatePath /p $env:CHALK_SIGNING_CERTIFICATE_PASSWORD $Path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $Path" }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode verification failed for $Path with status $($signature.Status)."
    }
}

try {
    if ($env:CHALK_SIGNING_CERTIFICATE_BASE64) {
        if (-not $env:CHALK_SIGNING_CERTIFICATE_PASSWORD) {
            throw 'CHALK_SIGNING_CERTIFICATE_PASSWORD is required when a signing certificate is provided.'
        }

        $signTool = (Get-Command signtool.exe -ErrorAction SilentlyContinue)?.Source
        if (-not $signTool) {
            $kitsBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
            $signTool = Get-ChildItem $kitsBin -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.DirectoryName -match '[\\/]x64$' } |
                Sort-Object FullName -Descending |
                Select-Object -First 1 -ExpandProperty FullName
        }
        if (-not $signTool) { throw 'signtool.exe was not found.' }

        $certificateName = "chalk-signing-$([Guid]::NewGuid().ToString('N')).pfx"
        $certificatePath = Join-Path ([System.IO.Path]::GetTempPath()) $certificateName
        [System.IO.File]::WriteAllBytes(
            $certificatePath,
            [Convert]::FromBase64String($env:CHALK_SIGNING_CERTIFICATE_BASE64))
    }

    if ($certificatePath) {
        Sign-Artifact $publishedExe
    } else {
        Write-Warning 'Signing credentials were not provided; artifacts will be unsigned.'
    }

    & $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $repoRoot 'installer/chalk.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

    $installer = Join-Path $repoRoot "dist/ChalkSetup-$version.exe"
    $portable = Join-Path $repoRoot "dist/Chalk-$version-portable.exe"
    Copy-Item $publishedExe $portable -Force
    Sign-Artifact $installer

    Write-Host "Built dist/ChalkSetup-$version.exe and dist/Chalk-$version-portable.exe"
} finally {
    if ($certificatePath) {
        Remove-Item -LiteralPath $certificatePath -Force -ErrorAction SilentlyContinue
    }
}
