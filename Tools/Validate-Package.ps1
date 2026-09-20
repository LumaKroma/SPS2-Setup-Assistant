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
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Generation\FullSetupGenerator.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Model\AttachmentBackendRegistry.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\Model\UndoComponentRegistration.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.asmdef',
    'Packages\com.lumakroma.sps2-setup-assistant\Editor\ModularAvatar\ModularAvatarAttachmentBackend.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\AttachmentBackendRegistryTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\ModularAvatar\LumaKroma.Sps2SetupAssistant.Editor.ModularAvatar.Tests.asmdef',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\ModularAvatar\ModularAvatarAttachmentBackendTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\FullSetupSettingsTests.cs',
    'Packages\com.lumakroma.sps2-setup-assistant\Tests\Editor\UndoComponentRegistrationTests.cs'
)

foreach ($relativePath in $requiredFiles) {
    $absolutePath = Join-Path $repoRoot $relativePath
    Assert-Condition (Test-Path -LiteralPath $absolutePath -PathType Leaf) "Missing required file: $relativePath"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Assert-Condition ($manifest.name -eq 'com.lumakroma.sps2-setup-assistant') 'Unexpected package ID.'
Assert-Condition ($manifest.version -eq '1.0.1') 'Unexpected release version.'
Assert-Condition ($manifest.unity -eq '2022.3') 'Unexpected Unity version.'
Assert-Condition ($manifest.license -eq 'MIT') 'Package license must be MIT.'
Assert-Condition ($manifest.vpmDependencies.'com.vrchat.avatars' -eq '>=3.10.4 <4.0.0') 'Unexpected VRChat SDK range.'
Assert-Condition ($manifest.vpmDependencies.'com.vrcfury.vrcfury' -eq '>=1.0.0') 'VRCFury installation must not force an exact version.'
Assert-Condition (-not ($manifest.vpmDependencies.PSObject.Properties.Name -contains 'nadena.dev.modular-avatar')) 'Modular Avatar must remain an optional dependency.'

Assert-Condition (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime/SetupSettings.cs')) 'Persistent authoring metadata is missing.'
$authoringFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Runtime') -Recurse -File -Filter '*.cs')
Assert-Condition ($authoringFiles.Count -eq 1 -and $authoringFiles[0].Name -eq 'SetupSettings.cs') 'Only shared settings data may ship outside Editor.'
$authoring = Get-Content -Raw -LiteralPath $authoringFiles[0].FullName
Assert-Condition (-not $authoring.Contains('MonoBehaviour')) 'Shared settings must not define an authoring component.'
Assert-Condition (-not [regex]::IsMatch($authoring, '\b(Awake|Start|Update|LateUpdate|FixedUpdate|OnEnable|OnDisable|OnAnimatorMove)\s*\(')) 'No custom runtime behavior is authorized.'

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Editor') -Recurse -File -Filter '*.cs')
$source = ($sourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$compatibilityFiles = @($sourceFiles | Where-Object { $_.FullName -like '*\Editor\Compatibility\*' })
$publicOnlySource = ($sourceFiles | Where-Object { $_.FullName -notlike '*\Editor\Compatibility\*' } | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$compatibility = ($compatibilityFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
Assert-Condition ($compatibility.Contains('VrcFuryCapabilities.Current') -and $compatibility.Contains('CanGenerate')) 'Capability guard is missing.'
Assert-Condition ($compatibility.Contains('RequireVersion()') -and $compatibility.Contains('SerializedPropertyType')) 'Version/schema validation is missing.'
Assert-Condition (Test-Path -LiteralPath (Join-Path $packageRoot 'Editor/Generation/Sps2SetupAsset.cs')) 'Editor settings storage is missing.'
$fullGenerator = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'Editor/Generation/FullSetupGenerator.cs')
Assert-Condition (-not [regex]::IsMatch($source, '\b(Sps2LegacySetupWindow|SocketSetupGenerator|Sps2SetupRoot)\b')) 'Retired PoC or migration code is present.'
Assert-Condition (-not [regex]::IsMatch($publicOnlySource, 'System\.Reflection|\bBindingFlags\b|\bGetField\s*\(|\bGetProperty\s*\(')) 'Unapproved reflection is present.'
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
    Assert-Condition (-not [regex]::IsMatch($publicOnlySource, $pattern)) "Dependency-private integration escaped Editor/Compatibility: $pattern"
}

Assert-Condition ($compatibility.Contains('CreateSocket') -and $compatibility.Contains('com.vrcfury.api.FuryComponents')) 'Public Socket API late binding is missing.'
Assert-Condition ($compatibility.Contains('CreateArmatureLink') -and -not $compatibility.Contains('BindingFlags.NonPublic')) 'Public-only attachment API binding is missing.'
Assert-Condition ($source.Contains('ModularAvatarBoneProxy')) 'Public Modular Avatar Bone Proxy integration is missing.'
Assert-Condition (($source.Contains('AttachmentBackendRegistry.TryApply') -and $source.Contains('Adapters.TryGetValue(backend, out var adapter)'))) 'Backend availability guard is missing.'
Assert-Condition ($source.Contains('Undo.RevertAllDownToGroup')) 'Atomic Undo rollback is missing.'
Assert-Condition ($source.Contains('Undo.RegisterCreatedObjectUndo(component, undoName)')) 'External factory components must be registered with Undo.'
Assert-Condition ($maSource.Contains('Undo.AddComponent<ModularAvatarBoneProxy>')) 'Modular Avatar components must be created through Undo.'

$fullCatalog = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'Editor/Model/FullSetupCatalog.cs')
Assert-Condition (([regex]::Matches($fullCatalog, 'Add\(setup, "')).Count -eq 15) 'The approved full catalog must have 15 fixed parts.'
foreach ($relative in @('Assets/IcePop/IcePop.fbx','Assets/IcePop/IcePop.mat','Documentation~/ASSET_PROVENANCE.md')) {
    Assert-Condition (Test-Path -LiteralPath (Join-Path $packageRoot $relative)) "Missing approved display asset/provenance: $relative"
}

& git -C $repoRoot diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'git diff --check failed for unstaged changes.'
}

& git -C $repoRoot diff --cached --check
if ($LASTEXITCODE -ne 0) {
    throw 'git diff --cached --check failed.'
}

Write-Output "PASS development metadata, optional MA, data-only authoring, isolated capability adapter, 15-part catalog, display asset provenance, Undo and diff whitespace"
