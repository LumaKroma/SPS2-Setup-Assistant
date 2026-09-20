[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Get-Content (Join-Path $root 'source.json') -Raw | ConvertFrom-Json
$releases = @()
for ($page=1; ; $page++) {
    # Public unauthenticated endpoint deliberately excludes private draft assets.
    $batch = @(Invoke-RestMethod "https://api.github.com/repos/LumaKroma/SPS2-Setup-Assistant/releases?per_page=100&page=$page" | ForEach-Object { $_ })
    $releases += $batch
    if ($batch.Count -lt 100) { break }
}
$urls = @(); $versions = @()
foreach ($release in $releases) {
    if ($release.draft) { continue }
    $version = [string]$release.tag_name
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { continue }
    $name = "com.lumakroma.sps2-setup-assistant-$version.zip"
    $asset = @($release.assets | Where-Object name -eq $name)
    if ($asset.Count -ne 1) { throw "Missing/ambiguous package ZIP in release $version" }
    if ($version -in $versions) { throw 'Duplicate version' }
    $urls += $asset[0].browser_download_url; $versions += $version
}
if ($urls.Count -eq 0) { throw 'No public package release exists yet; publish the validated draft first.' }
$source.githubRepos = @()
$source.packages = @(@{name='com.lumakroma.sps2-setup-assistant';releases=$urls})
$dir = Join-Path $root '.artifacts/listing-source'
[IO.Directory]::CreateDirectory($dir) | Out-Null
$source | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $dir 'source.json')
$versions | ConvertTo-Json -AsArray | Set-Content (Join-Path $dir 'expected-versions.json')
Copy-Item -LiteralPath (Join-Path $root 'Website') -Destination $dir -Recurse -Force
[IO.Directory]::CreateDirectory((Join-Path $root '.artifacts/site')) | Out-Null
