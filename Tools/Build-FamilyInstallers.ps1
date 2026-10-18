[CmdletBinding()]
param([Parameter(Mandatory)][string]$CreatorPath,[string]$OutputDirectory='.artifacts/family-installers')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$creator=[IO.Path]::GetFullPath($CreatorPath)
if ((Get-FileHash -LiteralPath $creator -Algorithm SHA256).Hash -ne '416709B3A70DD7B3023E84B04D077F6B38873B15FB7F1042C6900E72F00285AF') { throw 'Expected official VPAI creator 1.1.6' }
$out=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (-not $out.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside checkout' }
[IO.Directory]::CreateDirectory($out)|Out-Null
$version='1.2.0-beta.4'
$distribution=Join-Path $root "Distribution/$version"
foreach ($family in @('Fury','NDMF')) {
    $name="SPS2-Setup-Assistant-$version-$family-Installer"
    $package=Join-Path $out "$name.unitypackage"
    $zip=Join-Path $out "$name.zip"
    if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) { throw 'Use a fresh output directory; never overwrite candidate archives' }
    & node $creator (Join-Path $distribution "$family-config.json") $package
    if ($LASTEXITCODE -ne 0) { throw 'Official creator failed' }
    $archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in @($package,(Join-Path $distribution 'README.txt'),(Join-Path $distribution 'VPAI-LICENSE.txt'),(Join-Path $distribution 'VPAI-LICENSE-semver.net.txt'))) {
            $entry=$archive.CreateEntry([IO.Path]::GetFileName($file),[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime=[DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
            $stream=$entry.Open()
            try { $bytes=[IO.File]::ReadAllBytes($file); $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
    [ordered]@{family=$family;unitypackage=$package;zip=$zip;unitypackageSHA256=(Get-FileHash $package).Hash.ToLowerInvariant();zipSHA256=(Get-FileHash $zip).Hash.ToLowerInvariant()}|ConvertTo-Json -Compress
}
