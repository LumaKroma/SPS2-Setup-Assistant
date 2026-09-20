[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$VpmArchive,
    [Parameter(Mandatory)][string]$ExpectedSha256
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ((Get-FileHash -LiteralPath $VpmArchive -Algorithm SHA256).Hash -ine $ExpectedSha256) { throw 'VPM archive hash mismatch' }
$assetRoot = 'Assets/LumaKroma/SPS2SetupAssistant'
$destination = Join-Path $ProjectPath $assetRoot
if (Test-Path -LiteralPath $destination) { throw 'Destination already exists' }
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $VpmArchive))
try {
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -notmatch '^(Editor|Runtime|Assets)/|^(Editor|Runtime|Assets)\.meta$|^(package\.json|LICENSE\.md|README\.md)(\.meta)?$' -or
            $entry.FullName -match '(^|/)\.\.(/|$)|\\|:') { throw "Unexpected archive entry: $($entry.FullName)" }
    }
} finally { $archive.Dispose() }
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $VpmArchive), $destination)
$generator = Join-Path $destination 'Editor/Generation/FullSetupGenerator.cs'
$text = [IO.File]::ReadAllText($generator)
$old = 'public const string PackagePath = "Packages/com.lumakroma.sps2-setup-assistant";'
$new = 'public const string PackagePath = "Assets/LumaKroma/SPS2SetupAssistant";'
if ([regex]::Matches($text, [regex]::Escape($old)).Count -ne 1) { throw 'Expected exactly one package asset path declaration' }
[IO.File]::WriteAllText($generator, $text.Replace($old, $new), [Text.UTF8Encoding]::new($false))
$instructions = @'
# SPS2 Setup Assistant — unitypackage版

Unity 2022.3のVRChat Humanoidアバタープロジェクトで使用します。
先にVRChat Avatars SDK、VRCFury、lilToonを導入してください。
Modular Avatarは任意です。これらの依存パッケージは同梱していません。

VPM版とunitypackage版は同時に導入しないでください。
VPM版を導入済みの場合は、VCC/ALCOMで本ツールを削除してからインポートしてください。
unitypackage版からVPM版へ切り替える場合は、先に
Assets/LumaKroma/SPS2SetupAssistant フォルダーを削除してください。
切り替え前にはプロジェクトをバックアップしてください。

Unityへunitypackageをインポートし、全項目を選択してImportします。
Tools > LumaKroma > SPS2 Setup Assistant から対象アバターを選び、プレハブを生成します。
自動配置はアバターによって異なるため、生成後に位置・向きを確認してください。

更新は新しいunitypackageをインポートしてください。
自動更新を利用したい場合はVCC/ALCOM版をご利用ください。
https://lumakroma.github.io/SPS2-Setup-Assistant/

素材の参照先を固定しているため、上記のインポート先フォルダーは移動しないでください。
'@
$doc = Join-Path $destination 'UNITYPACKAGE.md'
[IO.File]::WriteAllText($doc, $instructions + "`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($doc + '.meta', "fileFormatVersion: 2`nguid: 4c742e34489b470e9a2eb942530c0547`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($destination + '.meta', "fileFormatVersion: 2`nguid: 1824306e4bd4439aaffbd9eab24486e5`nfolderAsset: yes`n", [Text.UTF8Encoding]::new($false))
$files = @(Get-ChildItem -LiteralPath $destination -File -Recurse) + @(Get-Item -LiteralPath ($destination + '.meta'))
$entries = @($files | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($ProjectPath,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
[ordered]@{assetRoot=$assetRoot;vpmSha256=$ExpectedSha256;pathReplacement=@{from=$old;to=$new};files=$entries} | ConvertTo-Json -Depth 6
