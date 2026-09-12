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

Each settings change creates a new snapshot, preserving previous snapshots for
Undo, saved Prefabs and discarded scene edits. Reusing the existing test Plug
does not save settings. Move the current settings asset and its `.meta` together
with the Prefab when transferring projects. A missing settings asset stops edits
and builds; the tool does not guess ownership or overwrite unknown objects.

Each part has an anchor and a child `Socket Pose` with a native VRCFury Socket.
The root can also contain `Guided Paths` and one `SPS2 Test Plug`.
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
| 上半身 | 左乳首, 右乳首, 胸の間 |
| 手 | 左手, 右手, 両手 |
| 下半身 | 膣, 肛門, ふとももの間 |
| 足 | 左足, 右足, 両足 |
| カスタム | User-named parts with avatar-descendant Transform references |

Casual selects mouth, chest middle, both hands and their middle, vagina and anus
(7). Default adds both feet, their middle and both nipples (12). Full adds ears
and thigh middle (15). Presets change fixed-part inclusion only; custom parts,
depth values and inactive action-type inputs survive switching.

Automatic placement uses avatar-aligned Humanoid measurements plus a temporary
baked body mesh. A uniquely selected body supplies surface rays and weighted
bone vertices; descriptor mouth viseme deltas locate the mouth center. Hand
placement uses wrist/finger measurements and feet use the sole surface. Paired
positions use the resulting individual poses. Missing or ambiguous surface
data falls back to bone measurements. Local +Z points inward; inspect and adjust
poses for each avatar. Surface estimates are not an anatomical guarantee.

Mouth uses Jaw with Head fallback. Ears use Head; pelvis uses Hips; hands use
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

## Penetration path

When enabled and both mouth/anus exist, two shared torso points follow
Chest/Spine and Hips. Separate exit points follow the opposing Socket pose.
Mouth uses Upper → Lower → Anus Exit; anus uses Lower → Upper → Mouth Exit.
Each direction has three stops, no shrink and native automatic tangents.

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

Japanese SPS2 menus group fixed/custom parts by category, with at most eight
controls per page. The native SPS container is replaced by one SPS2 entry;
remaining native options and existing sockets are nested under it. Common Auto
and Legacy controls are moved, not duplicated, while existing individual
Socket persistence is preserved. With only one eligible native Socket, native
Auto has no useful selector and may be absent. More than 16 eligible Auto
Sockets, including existing ones, stops authoring/build.

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

Only owner-approved display model/material assets are included; original menus,
Tracker integrations and gimmick components are not copied.
See [asset provenance](ASSET_PROVENANCE.md) and [validation](VALIDATION.md).
