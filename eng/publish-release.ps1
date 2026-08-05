[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "osx-arm64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [string]$OutputRoot = "artifacts/release"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$normalizedVersion = $Version.TrimStart("v")
if ($normalizedVersion -notmatch "^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$") {
    throw "Version 必须是 SemVer 形式，收到：$Version"
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repoRoot $OutputRoot));
$stagingRoot = Join-Path $resolvedOutputRoot "FlowForge-$normalizedVersion-$RuntimeIdentifier";
$publishRoot = Join-Path $stagingRoot "app";
$archivePath = Join-Path $resolvedOutputRoot "FlowForge-$normalizedVersion-$RuntimeIdentifier";

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force;
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null;
New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null;

Push-Location $repoRoot;
try {
    dotnet restore FlowForge.sln --runtime $RuntimeIdentifier;
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore 失败，RID=$RuntimeIdentifier"
    }

    dotnet publish src/FlowForge.App/FlowForge.App.csproj --configuration Release --runtime $RuntimeIdentifier --self-contained true --no-restore -p:Version=$normalizedVersion --output $publishRoot;
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败，RID=$RuntimeIdentifier"
    }

    Copy-Item -LiteralPath README.md -Destination (Join-Path $stagingRoot "README.md");
    Copy-Item -LiteralPath CHANGELOG.md -Destination (Join-Path $stagingRoot "CHANGELOG.md");
    Copy-Item -LiteralPath samples -Destination (Join-Path $stagingRoot "samples") -Recurse;

    if ($RuntimeIdentifier -eq "win-x64") {
        $archivePath += ".zip";
        Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal;
    }
    else {
        $archivePath += ".tar.gz";
        tar -czf $archivePath -C $stagingRoot .;
        if ($LASTEXITCODE -ne 0) {
            throw "tar 打包失败，RID=$RuntimeIdentifier"
        }
    }
}
finally {
    Pop-Location;
}

if (-not (Test-Path -LiteralPath $archivePath)) {
    throw "没有生成发布产物：$archivePath"
}

Write-Output $archivePath;
