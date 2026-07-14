param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$ffmpeg = Join-Path $root 'tools\ffmpeg\bin\ffmpeg.exe'
$office = Join-Path $root 'tools\LibreOfficePortable\App\libreoffice\program\soffice.exe'

if (-not (Test-Path -LiteralPath $ffmpeg)) {
    throw 'FFmpeg was not found. Place it in tools\ffmpeg\bin and run this script again.'
}

if (-not (Test-Path -LiteralPath $office)) {
    throw 'LibreOffice Portable was not found. Install it to tools\LibreOfficePortable and run this script again.'
}

$output = Join-Path $root 'publish\RenameConvPortable'
dotnet publish (Join-Path $root 'RenameConv.csproj') --configuration Release --runtime $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $output

$toolsOutput = Join-Path $output 'tools'
New-Item -ItemType Directory -Path $toolsOutput -Force | Out-Null
Copy-Item (Join-Path $root 'tools\LibreOfficePortable') $toolsOutput -Recurse -Force

$readme = @(
    'RenameConv Portable',
    '',
    'Run RenameConv.exe with no arguments to monitor all available drives.',
    'To monitor one folder only: RenameConv.exe "C:\Folder"',
    '',
    'The program, FFmpeg, .NET Runtime, and LibreOffice Portable are included.',
    'Move or archive this entire folder; do not move individual files.',
    'For LibreOffice Portable, unpack to an ASCII-only path, for example C:\RenameConvPortable.'
)
Set-Content -LiteralPath (Join-Path $output 'README.txt') -Value $readme -Encoding utf8

Write-Host "Complete: $output"
