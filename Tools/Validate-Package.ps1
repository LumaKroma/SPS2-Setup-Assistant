[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageRoot = Join-Path $repoRoot 'Packages\com.lumakroma.sps2-setup-assistant'
$manifestPath = Join-Path $packageRoot 'package.json'

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$requiredFiles = @(
    'AGENTS.md',
    'CONTRIBUTING.md',
    'LICENSE',
    'README.md',
    'Packages\com.lumakroma.sps2-setup-assistant\package.json',
    'Packages\com.lumakroma.sps2-setup-assistant\LICENSE.md',
    'Packages\com.lumakroma.sps2-setup-assistant\Documentation~\OUTPUT_CONTRACT.md',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Generation\SocketSetupGenerator.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\SocketPlacementPlannerTests.cs'
)

foreach ($relativePath in $requiredFiles) {
    $absolutePath = Join-Path $repoRoot $relativePath
    Assert-Condition (Test-Path -LiteralPath $absolutePath -PathType Leaf) "Missing required file: $relativePath"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Assert-Condition ($manifest.name -eq 'com.lumakroma.sps2-setup-assistant') 'Unexpected package ID.'
Assert-Condition ($manifest.version -eq '0.1.0') 'Unexpected private PoC version.'
Assert-Condition ($manifest.unity -eq '2022.3') 'Unexpected Unity version.'
Assert-Condition ($manifest.license -eq 'MIT') 'Package license must be MIT.'
Assert-Condition ($manifest.vpmDependencies.'com.vrchat.avatars' -eq '>=3.10.4 <4.0.0') 'Unexpected VRChat SDK range.'
Assert-Condition ($manifest.vpmDependencies.'com.vrcfury.vrcfury' -eq '>=1.1401.0 <2.0.0') 'Unexpected VRCFury range.'
Assert-Condition ($manifest.vpmDependencies.'nadena.dev.modular-avatar' -eq '>=1.18.1 <2.0.0') 'Unexpected Modular Avatar range.'

Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime'))) 'The 0.1.0 package must remain Editor-only.'

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Editor') -Recurse -File -Filter '*.cs')
$source = ($sourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

$forbiddenPatterns = @(
    'System\.Reflection',
    '\bBindingFlags\b',
    '\bSerializedObject\b',
    '\bFindProperty\b',
    '\bUnityEditorInternal\b',
    '\bVF\.',
    '\bVRCFuryHaptic',
    '\bSpsNdmf\b',
    '\bGetField\s*\(',
    '\bGetProperty\s*\('
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-Condition (-not [regex]::IsMatch($source, $pattern)) "Forbidden dependency integration pattern found: $pattern"
}

Assert-Condition ($source.Contains('FuryComponents.CreateSocket')) 'Public VRCFury Socket creation call is missing.'
Assert-Condition ($source.Contains('FuryComponents.CreateArmatureLink')) 'Public VRCFury Armature Link call is missing.'
Assert-Condition ($source.Contains('ModularAvatarBoneProxy')) 'Public Modular Avatar Bone Proxy integration is missing.'
Assert-Condition ($source.Contains('Undo.RevertAllDownToGroup')) 'Atomic Undo rollback is missing.'

$catalogPath = Join-Path $packageRoot 'Editor\Model\SocketCatalog.cs'
$catalogText = Get-Content -Raw -LiteralPath $catalogPath
$presetCount = ([regex]::Matches($catalogText, 'new SocketPreset\(')).Count
Assert-Condition ($presetCount -eq 8) "Expected 8 Socket presets, found $presetCount."

& git -C $repoRoot diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'git diff --check failed for unstaged changes.'
}

& git -C $repoRoot diff --cached --check
if ($LASTEXITCODE -ne 0) {
    throw 'git diff --cached --check failed.'
}

Write-Output "PASS package metadata, Editor-only boundary, 8-preset catalog, public-API guard, Undo guard, and diff whitespace"
