# SPS2 Setup Assistant package

Private development candidate `0.2.0-dev.1`.
Open `Tools > LumaKroma > SPS2 Setup Assistant`, select a Humanoid
`VRCAvatarDescriptor`, select a preset and adjust the included parts, then use
`プレハブを生成`. After generation, `プレハブを再生成` restores automatic placement and
`置き換えずに変更を反映` preserves existing Socket poses. `テストプラグ出現` adds the
owned IcePop Plug, which remains in the uploaded avatar when present.

The one-column UI supports 15 fixed parts, custom Transforms, typed depth
actions, penetration paths and native VRCFury integration. VRCFury `1.1403.0`
is required by the guarded compatibility adapter. Modular Avatar is optional;
enable its attachment option only when a supported MA `1.x` package is installed.
The test Plug material separately requires lilToon.

Read the [output contract](Documentation~/OUTPUT_CONTRACT.md),
[validation results and remaining gates](Documentation~/VALIDATION.md), and
[display asset provenance](Documentation~/ASSET_PROVENANCE.md).