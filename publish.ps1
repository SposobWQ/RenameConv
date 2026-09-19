param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$mainProject = Join-Path $root 'RenameConv.csproj'
$updaterProject = Join-Path $root 'Updater\RenameConvUpdater.csproj'
[xml]$project = Get-Content $mainProject
$version = $project.Project.PropertyGroup.Version | Select-Object -First 1
$publishRoot = Join-Path $root 'publish'
$updaterOutput = Join-Path $publishRoot 'UpdaterTemp'

if (-not (Test-Path (Join-Path $root 'tools\ffmpeg\bin\ffmpeg.exe'))) {
    throw 'FFmpeg was not found. Place it in tools\ffmpeg\bin and run this script again.'
}

if (-not (Test-Path (Join-Path $root 'tools\LibreOfficePortable\App\libreoffice\program\soffice.exe'))) {
    throw 'LibreOffice Portable was not found. Place it in tools\LibreOfficePortable and run this script again.'
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
if (Test-Path $updaterOutput) { Remove-Item $updaterOutput -Recurse -Force }

dotnet publish $updaterProject --configuration Release --runtime $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $updaterOutput

foreach ($edition in @('Portable', 'Online')) {
    $output = Join-Path $publishRoot "RenameConv$edition"
    $archive = Join-Path $publishRoot "RenameConv$edition-$version-$Runtime.zip"
    if (Test-Path $output) { Remove-Item $output -Recurse -Force }
    if (Test-Path $archive) { Remove-Item $archive -Force }

    $includeTools = if ($edition -eq 'Portable') { 'true' } else { 'false' }
    dotnet publish $mainProject --configuration Release --runtime $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeBundledTools=$includeTools --output $output
    Copy-Item (Join-Path $updaterOutput 'RenameConvUpdater.exe') $output -Force

    if ($edition -eq 'Portable') {
        $toolsOutput = Join-Path $output 'tools'
        New-Item -ItemType Directory -Path $toolsOutput -Force | Out-Null
        Copy-Item (Join-Path $root 'tools\ffmpeg') (Join-Path $toolsOutput 'ffmpeg') -Recurse -Force
        Copy-Item (Join-Path $root 'tools\LibreOfficePortable') (Join-Path $toolsOutput 'LibreOfficePortable') -Recurse -Force
    }

    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -CompressionLevel Optimal
    Write-Host "Complete: $archive"
}

Remove-Item $updaterOutput -Recurse -Force
