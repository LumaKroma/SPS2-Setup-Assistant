# Issue 121 development output contract

Applies to the approved `0.2.0-dev.1` implementation delta from baseline
`94596332f0245e131cbb83b092076835f691719f`, branch `issue/121-private-poc`.
It supersedes the eight-part PoC contract for the main setup window. The legacy
`SPS2 Setup Assistant (0.1 PoC)` window remains available separately.

## Authoring ownership

The owned root is a direct avatar child named `SPS2`. Newly generated roots
contain no package-defined component. Editor-only settings snapshots live in
`Assets/SPS2Settings/*.asset`; their asset GUID is recorded in native Socket
identifiers. Avatar-relative paths restore scene references, and asset GUID/local
IDs restore clip references. Ambiguous paths stop saving.

The window remembers the chosen scene avatar with GlobalObjectId across reload/play. A lost reference resolves the same target; an explicit re-detect button can bind the current selection or a unique generated avatar. Missing targets and Play mode explain why authoring is unavailable.

Each settings change creates a new snapshot, preserving previous snapshots for
Undo, saved Prefabs and discarded scene edits. Reusing the existing test Plug
does not save settings. Move the current settings asset and its `.meta` together
with the Prefab when transferring projects. A missing settings asset stops edits
and builds; the tool does not guess ownership or overwrite unknown objects.

Each part has an anchor and a child `Socket Pose` with a native VRCFury Socket.
The root can also contain `Guided Paths`, one `SPS2 Test Plug` and one independent `SPS2 Long Test Plug`.
No custom runtime script executes or is attached by generation. The old data-only
`Sps2SetupRoot` type remains solely for migration: Apply or Show Test Plug moves
its settings to an asset, removes it and renames its root in one Undo group.

Before authoring changes, all existing non-null owned references must remain
inside their expected root/anchor boundary. Duplicate identities, ambiguous
roots and external references fail without mutation. Missing owned objects can
be reconstructed by regeneration. The eight-part PoC root requires regeneration
to migrate; nonreplacement application does not migrate it.

Generation uses one Undo group. Regeneration keeps the selected applied
settings chosen in the window and existing Plug, and rebuilds automatic poses.
Nonreplacement application preserves surviving anchors/poses and manual
positions/rotations, adds/removes selected parts, and applies changed settings.
Changing the attachment backend requires regeneration. Changing a custom target
preserves its current world pose while rebinding its anchor.

Settings snapshots are saved with the operation; the scene/Prefab stores which
snapshot applies. The button wording does
not imply an automatic standalone Prefab asset export.

## Parts and presets

| Category | Fixed parts |
| --- | --- |
| 顔 | 口, 左耳, 右耳 |
| 上半身 | 左乳首, 右乳首, 胸 |
| 手 | 左手, 右手, 両手 |
| 下半身 | 膣, 肛門, ふとももの間 |
| 足 | 左足, 右足, 両足 |
| カスタム | User-named parts with avatar-descendant Transform references |

Casual selects mouth, chest middle, both hands and their middle, vagina and anus
(7). Default adds both feet, their middle and both nipples (12). Full adds ears
and thigh middle (15). Presets change fixed-part inclusion only; custom parts,
depth values and inactive action-type inputs survive switching.

Automatic placement uses avatar-aligned Humanoid measurements plus temporary
baked mesh measurements. Body selection counts actual vertex influence across
major bones; a bones array containing unused entries is not body coverage.
Descriptor viseme mesh supplies mouth and ear measurements. Temporary skin
copies exclude `Shrink*` clothing-hiding shapes, retain other shape values
(including heel pose), and never change the source avatar. Queries apply the
renderer world transform including scale. A missing/ambiguous body reports a
bone-estimate warning. Surface estimates are not an anatomical guarantee.

Nipples use the breast surface. Pelvis sockets use the underside surface near
the hips, with sagittal outward normals. The anus ray is .055 body-height units
behind Hips, with .003 body-height surface clearance; this is a bounded estimate. Cleavage points down so its ring plane
is horizontal. Hands run along the palm and feet along the sole toward the toes;
foot extents include weighted descendants when Humanoid Toes is unmapped.
Native radius offset is enabled on cleavage, hands and feet (including pairs),
with local +Y away from the surface. Local +Z points outward for openings or
along the intended tangent path; paired poses average individual results.
Existing manual poses and native radius offsets survive nonreplacement Apply.
Use regeneration to adopt these automatic-placement changes on older setups.

Mouth keeps its viseme-derived surface position. Its outward direction follows
the sagittal face-profile normal from viseme-mesh surface samples at +/-0.012
avatar height above/below the mouth. Pitch is bounded to +/-45 degrees; missing
samples retain the prior forward direction. This avoids using an unstable
single lip-triangle normal. Generation/regeneration adopts the angle, while
nonreplacement preserves manual rotations. The reverse penetration exit keeps
the same position and opposite direction. Mouth uses Jaw with Head fallback.
Ears use Head; pelvis uses Hips; hands use
Hand; feet use Toes with Foot fallback. Nipples prefer uniquely identified
skinned-mesh breast bones and otherwise estimate from UpperChest/Chest/Spine.
Breast identification normalizes punctuation/case in a bounded set of
Breast/Bust/Mune left/right names; ambiguity is treated as missing.

Chest middle uses the two breast bones, not upper arms. Hand/thigh/foot middles
use paired limb bones. Parent Constraints follow paired positions and rotations;
custom/non-Humanoid anchors also use Parent Constraints. Missing required
references skip the affected part. Inspect and adjust poses for each avatar.

Humanoid anchors use the existing public VRCFury Armature Link or optional
Modular Avatar Bone Proxy route. The optional MA assembly stays separate from
the core assembly. MA may reparent anchors before the native VRCFury callback,
so build-time ownership uses unique direct references beneath the avatar.

## Depth actions

One tool-managed depth group per selected part supports multiple actions:
BlendShape (renderer, shape, 0–100 weight), Animation Clip, or object ON/OFF.
Switching action type retains its other inputs.

Mouth initially detects descriptor Viseme `oh`, with weight 100. A missing
mesh/shape, clip or object skips that action and reports it. Defaults are
range `0..0.05`, meters, self interaction enabled, smoothing 0 and no reverse.
Under the observed native convention, 0.05 m outside is zero and the entrance
is full strength. Plug-length and local units remain selectable.

The range control spans -1 through 3 with a nonlinear scale concentrated near
zero; numeric fields use the same bounds. The near/far values are ordered.
Native SPS owns contact loss/reacquire, disable and expression competition.
No additional hold timer or restoration guarantee is introduced.

Native fields outside the managed group/name/common flags are retained.
Unchanged depth settings do not rewrite the native group. If multiple depth
groups were added manually, changing that part's managed depth configuration
stops instead of deleting the extra groups.

The setup window provides hover tooltips for 貫通, Auto Mode, 後方互換性 and
インスタント起動, describing their effects and use conditions. Each tooltip has
a visible help/question icon at the right of its row; label and icon expose the
same explanation. The Instant authoring option defaults OFF for new setups;
saved explicit ON/OFF preferences remain unchanged. Loading settings
upgrades only the built-in chest's former default label 胸の間 to 胸; custom
parts/names and internal IDs remain unchanged. Apply persists the updated label
to generated menus; changing this label does not require pose regeneration.

## Penetration path

When enabled and both mouth/anus exist, each direction has its own Neck (Head fallback with warning)
and Hips waypoint frames. Mouth uses Throat → Lower → Anus Exit; anus uses
Lower → Throat → Mouth Exit. Each direction has three stops. The throat point
uses the midpoint of the body's front/back intersections at Neck height, falling
back to the bone position if either intersection is unavailable. The oral cubic
enters inward and slightly upward before turning down through that point.
For mouth-to-throat distance d, its world controls are mouth - forward*0.8d +
up*0.2d and throat + up*0.8d + forward*0.16d. The reverse path uses the same controls
in reverse order. Other segments retain native automatic tangents.
Native path travel is -Z, so waypoint frames point
opposite the local route direction instead of inheriting bone rotations.
The setup checkbox `体内で太さを0にする` is nested under `貫通`, visible only
while penetration is enabled, and defaults ON (including older settings assets).
It controls native `Collapse plug between ...` on every internal segment in both
directions. This is an authoring option, not an added runtime menu control.
Its preference persists while the parent is OFF. Changing only this checkbox
updates native collapse flags without replacing manually adjusted points or
custom tangents; Undo/Redo includes the external settings snapshot.
When ON, native SPS collapses the internal cross-section to
zero radius, while the terminal RingOneWay preserves normal width outside the
opposing exit. This affects the Socket path, not a test Plug diameter setting.
When OFF, the same centered path keeps normal width; a plug wider than the neck
cannot be fully concealed by changing the path alone. The standard/long Plug
geometry and UI remain unchanged.
Exit positions follow the opposing Socket pose exactly; their +Z is reversed
from that entrance so the plug travels outwards. These changes require path
rebuilding/regeneration; unchanged nonreplacement keeps manually adjusted paths.
Stops are separate from Socket subtrees to meet the native Guided Path contract.
Rebuilding/removing the path clears native references first. Unchanged settings
retain manually adjusted paths; structural changes rebuild the affected path
configuration. Actual deformation and contact behavior still require runtime
validation.

## Menu, persistence and Instant flow

Build callbacks at -10001 and -9999 surround observed VRCFury processing.
The exact `1.1403.0` guard and required serialized-property schema isolate
private dependency integration in `Editor/Compatibility`.

On the build clone, collision-checked temporary Socket labels identify the
actual generated controls. Their parameters are read from the native controls;
no generated prefix is guessed. Expression booleans and native FX booleans or
floats are checked separately. Source controllers, menus and parameters are
not directly edited.

| Control | Initial value | Saved |
| --- | --- | --- |
| Owned individual Socket | OFF | No |
| Auto Mode | OFF | Yes |
| Legacy Compatibility | ON | Yes |
| Instant button | OFF | No |
| Local Only (native Stealth) | OFF | No |

The SPS2 menu begins in this order: 設定, 口, 胸, 膣, 肛門, 右手, 左手,
両手. Remaining generated fixed/custom sockets follow directly in catalog order;
there is no その他 submenu. Excluded sockets are omitted. Each page has at most
eight controls including 次へ. Full selects 15 sockets and uses three pages.
設定 contains Auto Mode, Legacy Compatibility and Instant when included.
The authoring Local Only checkbox is unchecked by default. Checking it exposes
the existing native Stealth control under 設定 as Local Only; unchecking removes
that control. In either case its native parameter starts OFF and is unsaved.
Native local-haptics/invisible-to-others behavior is reused without a new graph.

The native SPS container becomes one SPS2 entry. Remaining native options and
pre-existing sockets remain under 設定/標準設定・既存Socket. Common controls
are moved, not duplicated; existing individual Socket persistence is preserved.
With only one eligible native Socket, native Auto may be absent. More than 16
eligible Auto Sockets, including existing ones, stops authoring/build.

Instant has no duration/Exit Time hold:

Its states match the native FX Write Defaults policy. WD OFF requires a nonempty
constant animation on a build-only inactive marker; WD ON uses no empty WD OFF
clip. This prevents the Instant layer from latching native Socket activation.

| State | Entry/ownership | Exit |
| --- | --- | --- |
| Ready (initial) | Does not write Socket values | Press with Legacy ON → DeniedHeld; press with Legacy OFF/excluded → Fire |
| Fire | Local SDK parameter driver sets existing owned mouth/vagina parameters to 1 once | Still pressed → Held; released → Ready |
| Held | Holds no Socket values and does not repeat the driver | Release → Ready |
| DeniedHeld | Writes nothing; Legacy changing while held does not activate | Release → Ready |

Transitions have duration 0, no Exit Time, and no transition interruption.
Other Socket/Auto/Legacy parameters are unchanged. Native Auto scanning or
competing expressions can subsequently change native outputs. Reload restores
saved Auto/Legacy, starts owned Socket/Instant OFF and does not replay a press.
Final parameter-capacity validation belongs after native compression.

## Test Plug and validation

The same IcePop display Plug is reused and repositioned in front of the avatar.
It is included in upload when present, has no EditorOnly tag and is not removed
with the metadata. Its creation/repositioning supports Undo. Removing the setup
root removes it.

A separate 貫通テストプラグ出現 button creates/reuses a blue capsule with native
SPS Plug behavior. Its mesh is 1.5 m long and .05 m wide at avatar scale 1,
with 3729 vertices (96 longitudinal shaft segments) for deformation. Its tip
starts .08 world meters in front of the current mouth Socket, pointing inwards.
A missing mouth prevents this operation with an explanation. Move the Plug
using Unity's transform tools during native testing; no automatic insertion or
custom runtime simulator is introduced. Both Plugs persist across regeneration,
support Undo and are included in builds when present. Removing the root removes
both. The capsule's mesh/material are persistent subassets of its creation
settings snapshot. Transfer the Prefab with all referenced asset dependencies,
including that snapshot even if later settings snapshots have been created.

Only owner-approved display model/material assets are included; original menus,
Tracker integrations and gimmick components are not copied.
See [asset provenance](ASSET_PROVENANCE.md) and [validation](VALIDATION.md).
