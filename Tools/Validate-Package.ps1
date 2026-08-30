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
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Model\AttachmentBackendRegistry.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Model\UndoComponentRegistration.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.asmdef',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\ModularAvatar\ModularAvatarAttachmentBackend.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\AttachmentBackendRegistryTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.Tests.asmdef',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\ModularAvatar\ModularAvatarAttachmentBackendTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\SocketPlacementPlannerTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\UndoComponentRegistrationTests.cs'
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
Assert-Condition (-not ($manifest.vpmDependencies.PSObject.Properties.Name -contains 'nadena.dev.modular-avatar')) 'Modular Avatar must remain an optional dependency.'

Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime'))) 'The 0.1.0 package must remain Editor-only.'

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Editor') -Recurse -File -Filter '*.cs')
$source = ($sourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$coreAssemblyPath = Join-Path $packageRoot 'Editor\LumaKroma.Sps2SetupAssistant.Editor.asmdef'
$maAssemblyPath = Join-Path $packageRoot 'Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.asmdef'
$maTestsAssemblyPath = Join-Path $packageRoot 'Tests\Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.Tests.asmdef'
$coreAssembly = Get-Content -Raw -LiteralPath $coreAssemblyPath | ConvertFrom-Json
$maAssembly = Get-Content -Raw -LiteralPath $maAssemblyPath | ConvertFrom-Json
$maTestsAssembly = Get-Content -Raw -LiteralPath $maTestsAssemblyPath | ConvertFrom-Json
$coreSourceFiles = @($sourceFiles | Where-Object { $_.FullName -notlike '*\Editor\ModularAvatar\*' })
$coreSource = ($coreSourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$maSourceFiles = @($sourceFiles | Where-Object { $_.FullName -like '*\Editor\ModularAvatar\*' })
$maSource = ($maSourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

Assert-Condition (-not ($coreAssembly.references -contains 'nadena.dev.modular-avatar.core')) 'The core Editor assembly must not reference Modular Avatar.'
Assert-Condition ($maAssembly.references -contains 'nadena.dev.modular-avatar.core') 'The optional MA assembly must reference the public core assembly.'
Assert-Condition ($maAssembly.defineConstraints -contains 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'The optional MA assembly must be conditionally compiled.'
$maVersionDefine = @($maAssembly.versionDefines | Where-Object { $_.name -eq 'nadena.dev.modular-avatar' })
Assert-Condition ($maVersionDefine.Count -eq 1) 'The optional MA assembly must have one MA version define.'
Assert-Condition ($maVersionDefine[0].expression -eq '[1.18.1,2.0.0)') 'Unexpected optional Modular Avatar range.'
Assert-Condition ($maVersionDefine[0].define -eq 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'Unexpected optional Modular Avatar define.'
Assert-Condition ($maTestsAssembly.defineConstraints -contains 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'Optional MA tests must be conditionally compiled.'
Assert-Condition ($maTestsAssembly.defineConstraints -contains 'UNITY_INCLUDE_TESTS') 'Optional MA tests must remain test-only.'
Assert-Condition (-not [regex]::IsMatch($coreSource, 'nadena\.dev\.modular_avatar|\bModularAvatarBoneProxy\b')) 'The core Editor source must not compile against Modular Avatar.'
Assert-Condition ([regex]::IsMatch($maSource, '\bModularAvatarBoneProxy\b')) 'The optional public Modular Avatar integration is missing.'

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
Assert-Condition ($source.Contains('AttachmentBackendRegistry.IsAvailable')) 'Backend availability guard is missing.'
Assert-Condition ($source.Contains('Undo.RevertAllDownToGroup')) 'Atomic Undo rollback is missing.'
Assert-Condition ($source.Contains('Undo.RegisterCreatedObjectUndo(component, undoName)')) 'External factory components must be registered with Undo.'
Assert-Condition ($maSource.Contains('Undo.AddComponent<ModularAvatarBoneProxy>')) 'Modular Avatar components must be created through Undo.'

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

Write-Output "PASS package metadata, optional-MA boundary, Editor-only boundary, 8-preset catalog, public-API guard, Undo guard, and diff whitespace"
