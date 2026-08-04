param(
    [Parameter(Mandatory = $true)]
    [string] $CoverageRoot,

    [double] $MinimumPercent = 93.08
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-CoverageSummary {
    param(
        [string] $Message
    )

    Write-Host $Message
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $Message -Encoding utf8
    }
}

if (-not (Test-Path -LiteralPath $CoverageRoot -PathType Container)) {
    throw "覆盖率目录不存在：$CoverageRoot"
}

$coverageFiles = @(Get-ChildItem -LiteralPath $CoverageRoot -Recurse -Filter 'coverage.cobertura.xml' -File)
if ($coverageFiles.Count -eq 0) {
    throw "未找到 Cobertura 报告：$CoverageRoot"
}

$allLines = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$coveredLines = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($coverageFile in $coverageFiles) {
    $xml = [System.Xml.XmlDocument]::new()
    $xml.Load($coverageFile.FullName)

    foreach ($class in @($xml.SelectNodes('/coverage/packages/package/classes/class'))) {
        $fileName = [string] $class.GetAttribute('filename')
        $normalizedFileName = $fileName.Replace('\', '/')
        if ($normalizedFileName -notmatch '(?i)(^|/)FlowForge\.App/ViewModels/') {
            continue
        }

        foreach ($line in @($class.SelectNodes('./lines/line'))) {
            $lineNumber = [string] $line.GetAttribute('number')
            $lineHits = [int] $line.GetAttribute('hits')
            $key = '{0}:{1}' -f $normalizedFileName, $lineNumber
            [void] $allLines.Add($key)
            if ($lineHits -gt 0) {
                [void] $coveredLines.Add($key)
            }
        }
    }
}

if ($allLines.Count -eq 0) {
    throw 'Cobertura 报告中没有 FlowForge.App/ViewModels 行。'
}

$coveragePercent = 100.0 * $coveredLines.Count / $allLines.Count
$summary = 'ViewModels 行覆盖率：{0:N2}%（{1}/{2}，{3} 个报告）' -f `
    $coveragePercent, $coveredLines.Count, $allLines.Count, $coverageFiles.Count
Add-CoverageSummary $summary

if ($coveragePercent -lt $MinimumPercent) {
    throw ('ViewModels 行覆盖率 {0:N2}% 低于阈值 {1:N2}%。' -f $coveragePercent, $MinimumPercent)
}

Add-CoverageSummary ('覆盖率门禁通过：阈值 {0:N2}%。' -f $MinimumPercent)
