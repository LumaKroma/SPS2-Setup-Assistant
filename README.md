# SPS2 Setup Assistant

Private development package `0.2.0-dev.1` for configuring VRCFury SPS2 on Humanoid
VRChat avatars. The Japanese setup window now creates the setup, applies changes,
regenerates it and places a reusable IcePop test Plug.

This is the Issue 121 implementation delta from baseline
`94596332f0245e131cbb83b092076835f691719f`, on `issue/121-private-poc`.
It is not a published release. See the [output contract](Packages/com.lumakroma.sps2-setup-assistant/Documentation~/OUTPUT_CONTRACT.md)
and [validation record](Packages/com.lumakroma.sps2-setup-assistant/Documentation~/VALIDATION.md).

## Requirements

- Unity `2022.3`; observed Editor version `2022.3.22f1`.
- VRChat Avatars SDK `>=3.10.4 <4.0.0`; observed `3.10.4`.
- VRCFury: capability/schema detection, with native integration currently validated on `1.1403.0`. SPS1 warns and allows supported basic sockets; no SPS blocks generation. Unsupported options show update guidance. Older/newer release runtime validation remains separate.
- Optional Modular Avatar `>=1.18.1 <2.0.0` for the MA attachment route.
- The bundled IcePop material uses lilToon. Test-Plug placement requires its shader.

Add the local `Packages/com.lumakroma.sps2-setup-assistant` folder through your
development project's existing local-package workflow. No public VPM listing is
provided. A fresh VCC installation and additional dependency combinations still
need their own validation.

## Create and adjust a setup

1. Open `Tools > LumaKroma > SPS2 Setup Assistant`.
2. Select the scene or Prefab Mode avatar in `アバター`.
3. Choose `カジュアル`, `デフォルト` or `フル`, then change individual
   parts and depth actions as needed. Custom parts use a `Transform` reference.
4. Choose `貫通`, `Auto Mode`, `後方互換性` and `インスタント起動`.
   Enable `Modular Avatarで追従` only when the optional package is installed.
5. Click `プレハブを生成`. Inspect the generated root and adjust its ordinary
   `Socket Pose` transforms to fit the avatar.

After generation:

| Button | Result |
| --- | --- |
| `プレハブを再生成` | Rebuild from current settings and restore automatic placement; retain the existing test Plug. |
| `置き換えずに変更を反映` | Keep surviving Socket objects and manual poses while applying settings and part additions/removals. |
| `テストプラグ出現` | Create one test Plug or return that same Plug to the front of the avatar. |

Settings are saved as Editor assets under `Assets/SPS2Settings`; the generated
`SPS2` root contains no custom component. Save the scene or Prefab normally to
keep its snapshot reference. Transfer its current settings asset and `.meta`
alongside a Prefab when moving projects. Earlier snapshots are retained for
Undo and saved scene/Prefab revisions. The tool does not automatically create a separate
Prefab asset file. Remove the generated root to remove the authored setup and
its test Plug; use Undo to reverse an operation.

Missing bones, viseme inputs or custom references skip the affected item with a
message. An invalid avatar, foreign ownership reference, unsupported native
schema or Auto target count above 16 stops the transaction. A broken owned
setup can be regenerated; move an externally relocated owned object back before
doing so.

## Build and runtime boundary

The package reads its separate settings asset through native Socket identifiers.
Old attached metadata is supported for migration only. VRCFury supplies native
SPS behavior; public SDK callbacks configure
the generated menu, persistence and Instant Animator layer on the build clone.
`貫通` also works when only mouth or anus is enabled: the opposite standard
position becomes a bone-following exit without an additional Socket/menu item.
Under `貫通`, `体内で太さを0にする` defaults ON and controls native Collapse
for internal sections; normal width resumes outside the opposing Socket exit.
The oral curve enters the mouth before turning down through the measured neck
center. Regenerate to adopt the new route. Changing only the child checkbox
with nonreplacement apply preserves manual path positions and tangents.
OFF retains normal thickness on the same route; plugs wider than the neck can
still protrude. This setup option does not add a runtime menu control.
The test Plugs stay in uploaded avatars when present. A separate 貫通テストプラグ出現
button provides a long capsule in front of the mouth; move it with Unity transform
tools to inspect native penetration. Transfer its referenced mesh/material asset dependencies with the Prefab.

Owned Socket toggles start OFF and are unsaved. Auto starts OFF and is saved;
Legacy Compatibility starts ON and is saved. Instant is an unsaved button that
requests the generated mouth and vagina ON when Legacy is OFF or excluded.
The menu follows 設定 → 口 → 胸 → 膣 → 肛門 → 右手 → 左手 → 両手, followed by remaining sockets directly,
with 次へ pagination and no その他 submenu. Settings contains the three shared controls. Optional Local
Only reuses native Stealth, starts OFF and is unsaved; its setup checkbox defaults
to unchecked. Existing setups need regeneration to adopt corrected automatic
placement and native surface radius offsets. Nonreplacement keeps manual poses.
Existing individual Socket settings are preserved; shared native Auto/Legacy
controls remain shared with them.

[Validation](Packages/com.lumakroma.sps2-setup-assistant/Documentation~/VALIDATION.md)
records 21 passing EditMode tests, native SDK build observations, authoring
Undo/reload checks and Instant Animator transitions. These results do not replace
Gesture Manager, three-avatar visual review or exact-commit VRC PC testing.

## License and affiliation

Original code and documentation remain MIT licensed. VRCFury, Modular Avatar,
VRChat SDK and lilToon are separate dependencies and are not redistributed.
The owner-approved IcePop display assets have a separate
[provenance and distribution scope](Packages/com.lumakroma.sps2-setup-assistant/Documentation~/ASSET_PROVENANCE.md);
their inclusion does not grant a new public asset license.

This unofficial tool is not affiliated with or supported by those projects.
