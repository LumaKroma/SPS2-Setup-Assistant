Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$index = Get-Content (Join-Path $root '.artifacts/site/index.json') -Raw | ConvertFrom-Json
$expected = @(Get-Content (Join-Path $root '.artifacts/listing-source/expected-versions.json') -Raw | ConvertFrom-Json)
$versions = $index.packages.'com.lumakroma.sps2-setup-assistant'.versions
if ($index.author -isnot [string] -or [string]::IsNullOrWhiteSpace($index.author)) { throw 'Invalid listing author' }
if ($index.id -ne 'com.lumakroma.sps2-listing' -or $expected.Count -eq 0 -or (Compare-Object @($versions.PSObject.Properties.Name) $expected)) { throw 'Missing or unexpected listing versions' }
foreach ($p in $versions.PSObject.Properties) {
    if ($p.Value.version -ne $p.Name -or $p.Value.zipSHA256 -notmatch '^[a-fA-F0-9]{64}$' -or $p.Value.url -cne ("https://github.com/LumaKroma/SPS2-Setup-Assistant/releases/download/" + $p.Name + "/com.lumakroma.sps2-setup-assistant-" + $p.Name + ".zip")) { throw 'Invalid version manifest' }
}
'PASS all public versions included with archive hashes'
