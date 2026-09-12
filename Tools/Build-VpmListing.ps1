[CmdletBinding()]
param([string]$SourceDirectory='.artifacts/listing-source',[string]$OutputDirectory='.artifacts/site',[switch]$AllowLoopback)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath=[IO.Path]::GetFullPath((Join-Path $root $SourceDirectory))
$out=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$source=Get-Content (Join-Path $sourcePath 'source.json') -Raw | ConvertFrom-Json -AsHashtable
$id='com.lumakroma.sps2-setup-assistant'
if ($source.id -ne 'com.lumakroma.sps2-listing' -or $source.packages.Count -ne 1 -or $source.packages[0].name -ne $id) { throw 'Unexpected listing source' }
$expected=@(Get-Content (Join-Path $sourcePath 'expected-versions.json') -Raw | ConvertFrom-Json)
[IO.Directory]::CreateDirectory($out) | Out-Null
$versions=@{}
foreach ($url in $source.packages[0].releases) {
    $uri=[Uri]$url
    $file=[IO.Path]::GetFileName($uri.AbsolutePath)
    if ($file -notmatch '^com\.lumakroma\.sps2-setup-assistant-(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)\.zip$') { throw 'Unexpected ZIP name' }
    $version=$Matches[1]
    $canonical="https://github.com/LumaKroma/SPS2-Setup-Assistant/releases/download/$version/$file"
    $local=$AllowLoopback -and $uri.Scheme -eq 'http' -and $uri.Host -eq '127.0.0.1'
    if ($url -cne $canonical -and -not $local) { throw 'Unexpected release URL' }
    if ($versions.ContainsKey($version)) { throw 'Duplicate version' }
    $zip=Join-Path $out $file
    Invoke-WebRequest -Uri $url -OutFile $zip
    $archive=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entries=@($archive.Entries | Where-Object FullName -eq 'package.json')
        if ($entries.Count -ne 1) { throw 'ZIP must have exactly one root package.json' }
        $reader=[IO.StreamReader]::new($entries[0].Open())
        try { $manifest=$reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
    if ($manifest.name -cne $id -or $manifest.version -cne $version) { throw 'Manifest does not match release' }
    $manifest.url=$url
    $manifest.zipSHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $versions.Add($version,$manifest)
}
if ($versions.Count -eq 0 -or (Compare-Object @($versions.Keys) $expected)) { throw 'Incomplete release set' }
# A published version cannot disappear or silently change its archive.
if (-not $AllowLoopback) {
    $priorResponse=Invoke-WebRequest -Uri $source.url -SkipHttpErrorCheck
    if ($priorResponse.StatusCode -eq 200) {
        $prior=$priorResponse.Content | ConvertFrom-Json -AsHashtable
        foreach ($v in $prior.packages[$id].versions.Keys) {
            if (-not $versions.ContainsKey($v) -or $versions[$v].zipSHA256 -ne $prior.packages[$id].versions[$v].zipSHA256) { throw 'Published version missing or archive changed' }
        }
    } elseif ($priorResponse.StatusCode -ne 404) { throw 'Cannot verify existing listing' }
}
$source.Remove('githubRepos')
$source.author = $source.author.name
$source.packages=@{}; $source.packages[$id]=@{versions=$versions}
# JSON is emitted only after every archive passes validation.
$source | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'index.json')
Copy-Item (Join-Path $root 'Website/index.html') (Join-Path $out 'index.html') -Force
[pscustomobject]@{versions=$versions.Count;index=(Join-Path $out 'index.json')} | ConvertTo-Json -Compress
