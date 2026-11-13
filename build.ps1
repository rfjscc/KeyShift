param(
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fw 'WPF'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "Cannot find the .NET Framework compiler at $csc"
}

$outputDir = if ($Preview) { Join-Path $root 'preview' } else { Join-Path $root 'dist' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$output = if ($Preview) {
    Join-Path $outputDir 'KeyShift-Preview.exe'
} else {
    Join-Path $outputDir 'KeyShift.exe'
}

$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' |
    Sort-Object Name |
    ForEach-Object { $_.FullName })

$references = @(
    (Join-Path $fw 'System.dll'),
    (Join-Path $fw 'System.Core.dll'),
    (Join-Path $fw 'System.Xaml.dll'),
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll')
)

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/utf8output',
    '/codepage:65001',
    ('/out:' + $output),
    ('/win32icon:' + (Join-Path $root 'assets\app.ico')),
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    ('/resource:' + (Join-Path $root 'assets\app.ico') + ',app.ico')
)

if ($Preview) {
    $arguments += '/define:PREVIEW'
}

foreach ($reference in $references) {
    $arguments += ('/reference:' + $reference)
}

$arguments += $sources

$mode = if ($Preview) { 'preview' } else { 'release' }
Write-Host "Compiling $mode build..." -ForegroundColor Cyan
& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw ('Compilation failed. csc exit code: {0}' -f $LASTEXITCODE)
}

$file = Get-Item -LiteralPath $output
Write-Host ('Build OK: {0} ({1:N0} bytes)' -f $file.FullName, $file.Length) -ForegroundColor Green
