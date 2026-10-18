$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'bin\CodexUsageWidget.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run scripts/build.ps1 first.' }
$report = Join-Path ([System.IO.Path]::GetTempPath()) ('codex-usage-widget-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    $process = Start-Process -FilePath $exe -ArgumentList "--self-test `"$report`"" -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw 'Self-test process failed.' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($result.passed -ne 14) { throw 'Unexpected self-test results.' }
    Write-Output ($result | ConvertTo-Json -Depth 5)
} finally {
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
}
