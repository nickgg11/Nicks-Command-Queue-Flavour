# Builds the mod and packs the Thunderstore upload zip: <name>-<version>.zip in the repo root.
# Usage: powershell -ExecutionPolicy Bypass -File package.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number

# The plugin's own version should match the package's.
$pluginSource = Get-Content (Join-Path $root 'CommandQueue/CommandQueue.cs') -Raw
if ($pluginSource -notmatch 'BepInPlugin\([^)]*"([0-9.]+)"\)') { throw 'Could not find the BepInPlugin version.' }
if ($Matches[1] -ne $version) { throw "BepInPlugin version $($Matches[1]) doesn't match manifest.json version $version." }

dotnet build (Join-Path $root 'CommandQueue/CommandQueue.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# Zip entry name -> source file. Thunderstore wants these at the top of the zip.
$files = [ordered]@{
    'manifest.json'    = 'manifest.json'
    'README.md'        = 'README.md'
    'CHANGELOG.md'     = 'CHANGELOG.md'
    'icon.png'         = 'ReleaseAssets/icon.png'
    'CommandQueue.dll' = 'CommandQueue/bin/Release/netstandard2.1/CommandQueue.dll'
}

Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $root $files['icon.png']
$icon = [System.Drawing.Image]::FromFile($iconPath)
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256) { throw "icon.png must be 256x256, it is $($icon.Width)x$($icon.Height)." }
} finally { $icon.Dispose() }

$zipPath = Join-Path $root "$($manifest.name)-$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    foreach ($entry in $files.Keys) {
        $source = Join-Path $root $files[$entry]
        if (-not (Test-Path $source)) { throw "Missing $source" }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $source, $entry, 'Optimal') | Out-Null
    }
} finally { $zip.Dispose() }

Write-Host "Created $zipPath"
