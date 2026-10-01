[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$package = Join-Path $PSScriptRoot '../Packages/com.lumakroma.sps2-setup-assistant'
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $package 'Editor/Localization/Sps2Localization.cs')
$literal = '"(?:[^"\\]|\\.)*"'
$keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in ($source -split "`n" | Where-Object { $_ -match '^\s*\{ ".*new\[\]' })) {
    $values = @([regex]::Matches($line, $literal) | ForEach-Object { $_.Value | ConvertFrom-Json })
    if ($values.Count -ne 5 -or @($values | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) { throw "Incomplete translation row: $line" }
    if (-not $keys.Add($values[0])) { throw "Duplicate translation: $($values[0])" }
    foreach ($value in $values) { if ($value.Contains([char]0xFFFD)) { throw 'Invalid Unicode replacement character.' } }
    $placeholders = @([regex]::Matches($values[0], '\{\d+(?:[^}]*)\}') | ForEach-Object Value | Sort-Object) -join '|'
    foreach ($value in $values[1..4]) {
        $translated = @([regex]::Matches($value, '\{\d+(?:[^}]*)\}') | ForEach-Object Value | Sort-Object) -join '|'
        if ($translated -cne $placeholders) { throw "Mismatched placeholders: $($values[0])" }
    }
}
if ($keys.Count -lt 150) { throw 'Localization catalog is incomplete.' }
foreach ($file in (Get-ChildItem (Join-Path $package 'Editor') -Recurse -Filter '*.cs')) {
    if ($file.Name -eq 'Sps2Localization.cs') { continue }
    $text = Get-Content -Raw -Encoding UTF8 -LiteralPath $file.FullName
    foreach ($match in [regex]::Matches($text, ('\bL\((' + $literal + ')'))) {
        $key = $match.Groups[1].Value | ConvertFrom-Json
        if (-not $keys.Contains($key)) { throw "Missing translation in $($file.Name): $key" }
    }
}
foreach ($relative in @('Editor/Model/FullSetupCatalog.cs','Editor/Model/SocketDisplayNames.cs')) {
    $text = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $package $relative)
    foreach ($match in [regex]::Matches($text, $literal)) {
        $key = $match.Value | ConvertFrom-Json
        if ($key -match '[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]' -and $key -notin @('胸の間','左手の指輪','右手の指輪') -and -not $keys.Contains($key)) { throw "Missing standard-name translation: $key" }
    }
}
Write-Output "PASS $($keys.Count) localization entries, four translations each, literal call sites and standard names"
