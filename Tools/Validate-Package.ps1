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
Assert-Condition ($manifest.version -eq '2.0.0-beta.4') 'Unexpected release version.'
Assert-Condition ($manifest.unity -eq '2022.3') 'Unexpected Unity version.'
Assert-Condition ($manifest.license -eq 'MIT') 'Package license must be MIT.'
Assert-Condition ($manifest.vpmDependencies.'com.vrchat.avatars' -eq '>=3.10.4 <4.0.0') 'Unexpected VRChat SDK range.'
Assert-Condition (-not ($manifest.vpmDependencies.PSObject.Properties.Name -contains 'com.vrcfury.vrcfury')) 'VRCFury must remain an optional backend.'
Assert-Condition (-not ($manifest.vpmDependencies.PSObject.Properties.Name -contains 'net.nrzk.spsndmf')) 'SPS for NDMF must remain an optional backend.'
Assert-Condition (-not ($manifest.vpmDependencies.PSObject.Properties.Name -contains 'nadena.dev.modular-avatar')) 'Modular Avatar must remain an optional dependency.'
Assert-Condition ($manifest.PSObject.Properties.Name -contains 'legacyFolders') 'Legacy Assets migration metadata is missing.'
$legacyFolders = @($manifest.legacyFolders.PSObject.Properties)
Assert-Condition ($legacyFolders.Count -eq 1) 'Migration must target exactly one legacy product folder.'
Assert-Condition ($legacyFolders[0].Name -ceq 'Assets/LumaKroma/SPS2SetupAssistant' -and $legacyFolders[0].Value -ceq '1824306e4bd4439aaffbd9eab24486e5') 'Legacy migration path/GUID drift.'
Assert-Condition (-not ($manifest.PSObject.Properties.Name -contains 'legacyFiles') -and -not ($manifest.PSObject.Properties.Name -contains 'legacyPackages')) 'Additional migration deletion targets are not authorized.'

Assert-Condition (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime/SetupSettings.cs')) 'Persistent authoring metadata is missing.'
$authoringFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Runtime') -Recurse -File -Filter '*.cs')
$settingsPath = Join-Path $packageRoot 'Runtime/SetupSettings.cs'
$ownershipPath = Join-Path $packageRoot 'Runtime/Sps3OptionsOwnership.cs'
$expectedRuntime = @([IO.Path]::GetFullPath($settingsPath), [IO.Path]::GetFullPath($ownershipPath))
Assert-Condition ($authoringFiles.Count -eq 2 -and @($authoringFiles | Where-Object { $_.FullName -cnotin $expectedRuntime }).Count -eq 0) 'Unexpected Runtime source set.'
# This exact historical data-only type and MonoScript GUID must survive loading older scenes.
# Normalize checkout line endings before checking the approved source.
$ownershipText = [IO.File]::ReadAllText($ownershipPath).Replace("`r`n", "`n")
$ownershipHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($ownershipText)))
Assert-Condition ($ownershipHash -eq 'daee9a3f944baee545264eda04837f1b0b3a76034cbabad74bb1e749113aec2f') 'Unreviewed legacy ownership source.'
$ownershipMeta = [IO.File]::ReadAllText($ownershipPath + '.meta')
Assert-Condition ([regex]::Matches($ownershipMeta, '(?m)^guid: .*').Count -eq 1 -and [regex]::IsMatch($ownershipMeta, '(?m)^guid: 97e330bd1ebc425a88b679829deed7e2\r?$')) 'Legacy ownership script GUID mismatch.'
$authoring = Get-Content -Raw -LiteralPath $settingsPath
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
$coreSourceFiles = @($sourceFiles | Where-Object { $_.FullName -notlike '*\Editor\ModularAvatar\*' -and $_.FullName -notlike '*\Compatibility\Ndmf\*' })
$coreSource = ($coreSourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$maSourceFiles = @($sourceFiles | Where-Object { $_.FullName -like '*\Editor\ModularAvatar\*' })
$maSource = ($maSourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

Assert-Condition (-not ($coreAssembly.references -contains 'nadena.dev.modular-avatar.core')) 'The core Editor assembly must not reference Modular Avatar.'
Assert-Condition (-not ($coreAssembly.references -contains 'nadena.dev.ndmf')) 'The core Editor assembly must not reference NDMF.'
$nativeMaDefine = @($coreAssembly.versionDefines | Where-Object { $_.name -eq 'nadena.dev.modular-avatar' })
$nativeNdmfDefine = @($coreAssembly.versionDefines | Where-Object { $_.name -eq 'nadena.dev.ndmf' })
Assert-Condition ($nativeMaDefine.Count -eq 1 -and $nativeMaDefine[0].expression -eq '[1.18.1,2.0.0)' -and $nativeMaDefine[0].define -eq 'LUMAKROMA_SPS2_HAS_NATIVE_MA') 'Native MA version range is missing.'
Assert-Condition ($nativeNdmfDefine.Count -eq 1 -and $nativeNdmfDefine[0].expression -eq '[1.11.0,2.0.0)' -and $nativeNdmfDefine[0].define -eq 'LUMAKROMA_SPS2_HAS_NATIVE_NDMF') 'Native NDMF version range is missing.'
Assert-Condition ($maAssembly.references -contains 'nadena.dev.modular-avatar.core') 'The optional MA assembly must reference the public core assembly.'
Assert-Condition ($maAssembly.defineConstraints -contains 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'The optional MA assembly must be conditionally compiled.'
$maVersionDefine = @($maAssembly.versionDefines | Where-Object { $_.name -eq 'nadena.dev.modular-avatar' })
Assert-Condition ($maVersionDefine.Count -eq 1) 'The optional MA assembly must have one MA version define.'
Assert-Condition ($maVersionDefine[0].expression -eq '[1.18.1,2.0.0)') 'Unexpected optional Modular Avatar range.'
Assert-Condition ($maVersionDefine[0].define -eq 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'Unexpected optional Modular Avatar define.'
Assert-Condition ($maTestsAssembly.defineConstraints -contains 'LUMAKROMA_SPS2_HAS_MODULAR_AVATAR') 'Optional MA tests must be conditionally compiled.'
Assert-Condition ($maTestsAssembly.defineConstraints -contains 'UNITY_INCLUDE_TESTS') 'Optional MA tests must remain test-only.'
Assert-Condition (-not [regex]::IsMatch($coreSource, 'using\s+nadena\.dev\.modular_avatar|\b(?:typeof|GetComponent|AddComponent)<ModularAvatarBoneProxy>')) 'The core Editor source must not compile against Modular Avatar.'
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
Assert-Condition ($compatibility.Contains('net.nrzk.spsndmf') -and $compatibility.Contains('PackageMatches') -and $compatibility.Contains('SpsNdmf.Plugin.SpsNdmfPlugin') -and $compatibility.Contains('HasNativeNdmfBuildIntegration')) 'Dual-family package and native build boundaries are missing.'
Assert-Condition (-not $compatibility.Contains('CaptureNdmf') -and -not (Test-Path -LiteralPath (Join-Path $packageRoot 'Editor/Compatibility/Ndmf/Sps2NdmfPlugin.cs'))) 'Assistant NDMF build passes must be absent.'
Assert-Condition ($compatibility.Contains('CreateArmatureLink')) 'Public attachment API binding is missing.'
# Reviewed source snapshots each contain exactly one nonpublic, read-only lookup.
# No exemption for a directory, an assembly, another lookup, or changed source.
$reviewedNonPublic = @{
    'Editor/Compatibility/OwnedNativeMenuAuthoring.cs' = 'adacdc3dffc6fe878d134154e9b187ea505f7b6c0532ef8d4f89c9f6055c4cf4'
    'Editor/Compatibility/SpsMenuDestination.cs' = '14a4749dfd44dcacf9d3fe226c6dfb600c53d4eddd3da94da01058453fc36679'
    'Editor/Compatibility/Sps3MenuLayoutSources.cs' = '8be428670c83550cc34f43d346f3d735b4d6b7e303e3c723ef5bb5c4a265604e'
}
foreach ($file in $compatibilityFiles) {
    $code = [IO.File]::ReadAllText($file.FullName).Replace("`r`n","`n")
    $uses = [regex]::Matches($code, '\bNonPublic\b').Count
    if ($uses -eq 0) { continue }
    $rel = [IO.Path]::GetRelativePath($packageRoot,$file.FullName).Replace('\','/')
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($code)))
    Assert-Condition ($uses -eq 1 -and $rel -cin $reviewedNonPublic.Keys -and $hash -eq $reviewedNonPublic[$rel]) "Unreviewed nonpublic lookup: $rel"
}
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

& (Join-Path $PSScriptRoot 'Test-Localization.ps1')
Write-Output "PASS development metadata, optional MA, data-only authoring, isolated capability adapter, 15-part catalog, display asset provenance, Undo and diff whitespace"
