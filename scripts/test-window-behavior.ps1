param([string]$Executable,[string]$ReportPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $root 'bin\CodexUsageWidget.exe' }
if (-not $ReportPath) { $ReportPath = Join-Path $root 'work\window-behavior.json' }
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath)
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$harness = Join-Path (Split-Path -Parent $ReportPath) 'WindowBehaviorTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 "/out:$harness" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Xaml.dll "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\PresentationFramework.dll" "$root\tests\WindowBehaviorTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Window test harness did not compile.' }
$run = Start-Process -FilePath $harness -ArgumentList "`"$Executable`" `"$ReportPath`"" -WindowStyle Hidden -Wait -PassThru
$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
Write-Output ($report | ConvertTo-Json -Depth 5)
if ($run.ExitCode -ne 0 -or -not $report.passed) { throw 'Window behavior regression failed.' }
