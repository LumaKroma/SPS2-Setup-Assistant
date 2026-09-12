[CmdletBinding()]
param([string]$OutputDirectory = '.artifacts/vpm')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = Join-Path $root 'Packages/com.lumakroma.sps2-setup-assistant'
$manifest = Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
if ($manifest.version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid package version' }
$out = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
[IO.Directory]::CreateDirectory($out) | Out-Null
$zipPath = Join-Path $out ($manifest.name + '-' + $manifest.version + '.zip')
if (Test-Path -LiteralPath $zipPath) { throw 'Archive already exists; use a new output directory' }
$paths = [Collections.Generic.List[string]]::new()
foreach ($folder in @('Editor','Runtime','Assets')) {
    $paths.Add($folder + '.meta')
    foreach ($entry in Get-ChildItem -LiteralPath (Join-Path $package $folder) -Recurse -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point in package' }
        $rel = [IO.Path]::GetRelativePath($package, $entry.FullName).Replace('\','/')
        if ($entry.PSIsContainer) {
            if (-not (Test-Path -LiteralPath ($entry.FullName + '.meta'))) { throw "Missing directory meta: $rel" }
        } else {
            if ($entry.Extension -ne '.meta' -and -not (Test-Path -LiteralPath ($entry.FullName + '.meta'))) { throw "Missing asset meta: $rel" }
            $paths.Add($rel)
        }
    }
}
foreach ($name in @('package.json','LICENSE.md','README.md')) { $paths.Add($name); $paths.Add($name + '.meta') }
$ordered = $paths.ToArray(); [Array]::Sort($ordered, [StringComparer]::Ordinal)
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($rel in $ordered) {
        $file = Join-Path $package $rel
        $bytes = [IO.File]::ReadAllBytes($file)
        if ([Text.Encoding]::UTF8.GetString($bytes,0,[Math]::Min(130,$bytes.Length)).StartsWith('version https://git-lfs.github.com/spec/v1')) { throw "Unresolved LFS pointer: $rel" }
        if ($rel.EndsWith('.meta') -and -not (Test-Path -LiteralPath $file.Substring(0,$file.Length-5))) { throw "Orphan meta: $rel" }
        $entry = $archive.CreateEntry($rel,[IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $stream = $entry.Open(); try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
    }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest | Add-Member url ("https://github.com/LumaKroma/SPS2-Setup-Assistant/releases/download/" + $manifest.version + '/' + [IO.Path]::GetFileName($zipPath)) -Force
$manifest | Add-Member zipSHA256 $hash -Force
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $out 'package.json'),($manifest | ConvertTo-Json -Depth 12)+"`n",$utf8)
[IO.File]::WriteAllText((Join-Path $out 'SHA256SUMS'),"$hash  $([IO.Path]::GetFileName($zipPath))`n",$utf8)
# Verify root layout and exact source-byte equivalence after compression.
$read = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if ($read.Entries.Count -ne $ordered.Count -or $null -eq $read.GetEntry('package.json')) { throw 'Invalid ZIP root' }
    foreach ($entry in $read.Entries) {
        if ($entry.FullName -notin $ordered) { throw 'Unexpected archive entry' }
        $stream=$entry.Open(); $memory=[IO.MemoryStream]::new()
        try { $stream.CopyTo($memory); $actual=[Security.Cryptography.SHA256]::HashData($memory.ToArray()) }
        finally { $stream.Dispose(); $memory.Dispose() }
        $expected=[Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes((Join-Path $package $entry.FullName)))
        if ([Convert]::ToHexString($actual) -ne [Convert]::ToHexString($expected)) { throw 'Archive content mismatch' }
    }
} finally { $read.Dispose() }
[pscustomobject]@{zip=$zipPath;version=$manifest.version;sha256=$hash;files=$ordered.Count} | ConvertTo-Json -Compress
