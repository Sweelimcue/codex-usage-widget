param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'bin' }
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework x64 compiler is required.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:CodexMeter.Program "/out:$OutputDirectory\CodexUsageWidget.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\PresentationFramework.dll" "$root\src\CodexMeter.cs" "$root\src\RingMeter.cs" "$root\src\SkinController.cs" "$root\src\RenderPreviews.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output (Join-Path $OutputDirectory 'CodexUsageWidget.exe')
