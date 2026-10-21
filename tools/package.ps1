param([string]$GamePath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $GamePath) { $GamePath = $env:STARDEW_GAME_PATH }
if (-not $GamePath) {
    $candidate = Resolve-Path -LiteralPath (Join-Path $repoRoot '..\..') -ErrorAction SilentlyContinue
    if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate.Path 'Stardew Valley.dll'))) { $GamePath = $candidate.Path }
}
if (-not $GamePath) { $GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley' }
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'Stardew Valley.dll'))) {
    throw "Stardew Valley.dll was not found in $GamePath. Pass -GamePath explicitly."
}
$project = Join-Path $repoRoot 'src\GlobalKegChest\GlobalKegChest.csproj'
& dotnet build $project -c Release "-p:GamePath=$GamePath" -p:EnableModDeploy=false -p:EnableModZip=false
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$manifestPath = Join-Path $repoRoot 'src\GlobalKegChest\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Version -notmatch '^\d+\.\d+\.\d+(?:-beta\.\d+)?$') { throw 'Expected a numeric or beta release version.' }
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$output = Join-Path $dist "GlobalKegChest-$($manifest.Version).zip"
$build = Join-Path $repoRoot 'src\GlobalKegChest\bin\Release\net6.0'
$source = Join-Path $repoRoot 'src\GlobalKegChest'
$files = [ordered]@{
    'GlobalKegChest.dll' = Join-Path $build 'GlobalKegChest.dll'
    'GlobalKegChest.Core.dll' = Join-Path $build 'GlobalKegChest.Core.dll'
    'manifest.json' = $manifestPath
    'LICENSE' = Join-Path $repoRoot 'LICENSE'
    'assets/chest.png' = Join-Path $source 'assets\chest.png'
    'i18n/default.json' = Join-Path $source 'i18n\default.json'
    'i18n/zh.json' = Join-Path $source 'i18n\zh.json'
    'README.md' = Join-Path $repoRoot 'README.md'
    'CHANGELOG.md' = Join-Path $repoRoot 'CHANGELOG.md'
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$stream = [System.IO.File]::Open($output, [System.IO.FileMode]::Create)
$zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $files.GetEnumerator()) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value,
            "GlobalKegChest/$($entry.Key)", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose(); $stream.Dispose() }
Write-Output "Release package: $output"
Get-FileHash -LiteralPath $output -Algorithm SHA256 | Select-Object Hash,Path
