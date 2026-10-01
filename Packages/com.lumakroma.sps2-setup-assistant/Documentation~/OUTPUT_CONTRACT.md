# 1.1.0 localization and menu names — 2026-09-30 (unreleased)

## Paired adjustment Transform names

New standard left/right adjustment children use `SPS_EarSocket_L/R`,
`SPS_NippleSocket_L/R`, `SPS_HandSocket_L/R` and `SPS_FootSocket_L/R`.
The paired name belongs to the adjustable Socket pose, not the attachment anchor.
Anchor names, internal part IDs, menu names, placement, parent coordinate frames
and native behavior are unchanged. Center and custom parts retain `Socket Pose`.

Ordinary Apply never renames existing poses. Regeneration preserves the previous
pose name for each surviving part ID, including legacy and authored names, so a
package upgrade does not silently change those hierarchy paths. Newly added parts
use the new rule. Existing setups do not automatically migrate; changing a saved
hierarchy requires separately reviewing its settings paths and external animation
bindings. This change does not migrate or rewrite third-party AnimationClips.

The side suffixes permit name-based pairing in the inspected SymmetryBoneEditor
source. Its operations mirror local coordinates, so actual geometric symmetry
still depends on the avatar's left/right parent frames and axis settings. No
SymmetryBoneEditor installation, runtime integration or geometric guarantee is added.

## Language and display names

- UI language is a local Editor preference. Menu language is saved per avatar in
  the existing settings asset. Both support Japanese, English, Korean, Simplified
  Chinese and Traditional Chinese; Japanese is the default for old and new data.
- Every socket has an optional menu-name override. Nonblank text is preserved
  verbatim across language changes, presets, Copy, save/reload, Apply and
  regeneration. Blank/whitespace uses the standard name in the menu language.
  Reset clears the override. Custom parts fall back to their authored part name.
- Old nonstandard stored names migrate once to overrides. The historical chest
  alias remains normalized. Clearing a migrated override must not resurrect it.
- The assistant owns the final generated menu name. Edit it in the assistant;
  native Socket `Name in menu` is used for ownership/build identity and is not
  the final label. Apply or regenerate to save edited menu settings before build.
- Build output uses the saved menu language, independent of the current Editor
  UI language. Settings, Auto Mode, legacy compatibility, Local Only, preserved
  native-settings container and generated pagination labels are localized.
  Unrelated/native inner controls and authored custom names are preserved.
- Stable IDs, build tokens, references, modes, placement, depth actions,
  parameter names/defaults/persistence, ordering and pagination capacity remain
  unchanged. A menu-language/override-only Apply does not reconfigure sockets.
- Third-party exception text, native Inspector labels and internal asset/object
  names are outside UI localization. Translation and layout review in Unity is
  still required; source validation is not runtime certification.

# 1.0.2 socket mode amendment — 2026-09-22

Mouth and anus use Ring only when effective penetration is enabled; otherwise Auto.
Every Apply reconciles these two modes, including unchanged OFF settings from 1.0.1.
Other socket modes and manual poses remain unchanged. Mode changes participate in Undo.
Unsupported guided paths are effectively OFF and therefore use Auto.

# Breast selection repair — 2026-09-20

Breast sockets use bones referenced by the measured body skin, restricted to the
humanoid Chest subtree (UpperChest/Spine when Chest is absent). If the body skin
cannot be identified, the same torso restriction applies to all skin references.
Separate outfit armatures cannot compete with the body skeleton. Existing breast
name aliases also accept numeric segments; matching descendants resolve to their
matching ancestor root, even if only a descendant is referenced by the skin.
Independent ambiguous roots remain unresolved. Surface measurement, pose math,
native attachment and nonreplacement manual pose preservation are unchanged.
The central chest socket shares this resolver. Existing setups require regeneration
to adopt corrected automatic positions; ordinary Apply retains manual positions.
# 1.0.0 promotion — 2026-09-13

Owner approves current implementation as 1.0.0 and public VPM publication.
Metadata and README publication notices change; product behavior is unchanged
from 5ddff4837122d53526112f01d6f1c6732961cd65. Historical development version
references below are retained as history.

# Unpublished PoC removal — 2026-09-13

Only Tools/LumaKroma/SPS2 Setup Assistant is available. The old window, generator,
eight-part catalog/planner and old component metadata migration are removed.
Current settings data and Editor asset storage are retained. This supersedes
prior historical migration requirements. VRCFury SPS1/Legacy Compatibility is
unchanged; it is unrelated to the unpublished assistant version.

# Instant removal amendment — 2026-09-13

User requested removal after reporting VRC PC PASS for the preceding handoff.
Instant is no longer a setting or capability and creates no menu, parameter,
Animator layer, driver, clip or marker. Its dedicated WD policy and diagnostic
recognizer are removed. Older instant JSON fields are ignored, including true.
Existing authoring setups need only rebuild; regeneration is not required for
removal. Uploaded avatars require reupload. Other native menu controls, defaults,
persistence and Socket lifecycles remain unchanged. Historical Instant UI/flow
entries below and prior validation evidence are superseded, not current features.

# Mouth reference recovery amendment — 2026-09-13

On avatar binding, restore a missing mouth BlendShape renderer from the descriptor
when the retained shape is empty or matches its valid oh viseme. Preserve explicit
renderers, custom expressions, non-BlendShape actions, weights and depth toggles.
This repairs incomplete saved defaults without changing generated placement or
native depth action behavior. Existing generated actions require settings Apply.

# VPM packaging amendment — 2026-09-13

Distribution contains Assets/Editor/Runtime, their metadata, root package.json,
MIT LICENSE.md and Japanese README with metadata. Tests and Documentation~ are
development-only and excluded from ZIP. Product runtime/settings unchanged.
Release creation is a manual draft step; listing publication requires a public
repository and published, validated release. Version remains0.2.0-dev.1 pending
runtime/publication gates. Public indexes preserve all prior versions and hashes.

# Historical native diagnostic Write Defaults amendment — 2026-09-13

Superseded by removal of Instant and its WD-policy helper above.

Instant-start WD policy ignores VRCFury's observed preview-only empty-layer notice.
Recognition requires the (NO VALID ANIMATIONS) layer suffix, complete warning text
with whitespace normalized, null motion, no behaviours/outgoing transitions, not
being the default state, and no incoming state/AnyState/entry transition in its
machine. Ordinary mixed states still reject the build; no WD value or diagnostic
state is rewritten/deleted. Unknown diagnostic text remains conservatively counted.
Runtime Instant flow and existing Direct/Additive exceptions are unchanged.

# Chest depth amendment — 2026-09-13

User approved the MANUKA/Milltina/rurune scene trial. For the chest socket, when
both breast-tip surface measurements succeed, use the halfway forward depth
between the bilateral breast root midpoint and tip midpoint. Clamp this depth
not to move forward of the original centerline surface result. Preserve measured
height, horizontal position, orientation and following. Missing surface evidence
retains the previous fallback. Other socket placement is unchanged. Normal
nonreplacement settings changes retain manual placement; regeneration uses this
new default. This replaces front-contour depth with an interior breast depth.

# Oral Collapse boundary amendment — 2026-09-13

The owner accepted the MANUKA scene trial and requested adoption into generation.
The existing mouth-to-throat cubic is split at t=0.4 using de Casteljau subdivision.
The internal oral boundary replaces the waist stop to retain the native three-stop
limit: mouth → oral interior → throat → anus; reverse routing uses the same oral
control points in reverse. With internal Collapse enabled, mouth segments use
[false, true, true] and anus segments [true, true, false]. With Collapse disabled,
all segments remain uncollapsed. This moves the abrupt width boundary inside the
mouth; it does not add shader smoothing or guarantee concealment on every avatar.

Editor settings snapshots persist oralBoundaryPath. Missing legacy metadata is
false: Collapse-only edits retain legacy segment behavior until regeneration.
For adopted layouts, Collapse-only edits preserve manual waypoint positions and
tangents and the uncollapsed oral segment. Clearing the route resets this flag.
Single-ended generation retains virtual opposite endpoints without adding an
unselected Socket or menu entry. No dependency or custom runtime component change.

# IcePop presentation amendment — 2026-09-13

Normal IcePop show/re-show sets the owned Plug root localScale to (0.7, 0.7, 0.7).
Ice uses the original material; Stick uses the package's opaque brown lilToon material.
Undo restores previous scale/material assignments; object reuse and long capsule
behavior are unchanged. Included IcePop assets may be publicly distributed under
MIT per explicit owner permission; dependency licenses remain separate.
Native path/Collapse behavior is unchanged in this amendment.

# Issue 121 development output contract

Applies to the approved `0.2.0-dev.1` implementation delta from baseline
`94596332f0245e131cbb83b092076835f691719f`, branch `issue/121-private-poc`.
The unpublished eight-part PoC and its migration support have been removed.

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
No custom runtime script executes or is attached by generation. Only Editor asset
settings are supported; old component metadata is not migrated.

Before authoring changes, all existing non-null owned references must remain
inside their expected root/anchor boundary. Duplicate identities, ambiguous
roots and external references fail without mutation. Missing owned objects can
be reconstructed by regeneration. No PoC migration is provided.

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
a visible help/question icon immediately after its label; label and icon expose the
same explanation. Help is an antialiased circle and font-rendered question mark
inside an explicit inset rectangle, without a scaled bitmap or default label
padding. The help rectangle is shifted down2 GUI points to align with adjacent
toggle text. The Instant authoring option defaults OFF for new setups;
saved explicit ON/OFF preferences remain unchanged. Loading settings
upgrades only the built-in chest's former default label 胸の間 to 胸; custom
parts/names and internal IDs remain unchanged. Apply persists the updated label
to generated menus; changing this label does not require pose regeneration.

## Penetration path

When enabled and at least one mouth/anus Socket exists, each included direction
has its own Neck (Head fallback with warning)
and Hips waypoint frames. Mouth uses Throat → Lower → Anus Exit; anus uses
Lower → Throat → Mouth Exit. Each direction has three stops. If the opposing
Socket is excluded, its pose is calculated by the same standard placement logic
and retained only as a bone-following Transform under Guided Paths. No native
Socket, depth action, menu item or standalone interaction target is created for
that virtual endpoint. The endpoint/exit remain necessary to define the path;
the excluded Socket component is never instantiated. With both included, the
existing real Socket poses are used. With neither included, or penetration OFF,
there is no path. Inclusion changes rebuild the owned path; native references
are cleared before removing it. Unchanged settings and Collapse-only changes
preserve manual path adjustments; Undo/Redo restores the prior path/settings.
The throat point
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

## Menu and persistence

Build callbacks at -10001 and -9999 surround observed VRCFury processing.
Installed feature/schema checks and actual native build-output checks isolate
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
| Local Only (native Stealth) | OFF | No |

The SPS2 menu begins in this order: 設定, 口, 胸, 膣, 肛門, 右手, 左手,
両手. Remaining generated fixed/custom sockets follow directly in catalog order;
there is no その他 submenu. Excluded sockets are omitted. Each page has at most
eight controls including 次へ. Full selects 15 sockets and uses three pages.
設定 contains Auto Mode and Legacy Compatibility when included.
The authoring Local Only checkbox is unchecked by default. Checking it exposes
the existing native Stealth control under 設定 as Local Only; unchecking removes
that control. In either case its native parameter starts OFF and is unsaved.
Native local-haptics/invisible-to-others behavior is reused without a new graph.

The native SPS container becomes one SPS2 entry. Remaining native options and
pre-existing sockets remain under 設定/標準設定・既存Socket. Common controls
are moved, not duplicated; existing individual Socket persistence is preserved.
With only one eligible native Socket, native Auto may be absent. More than 16
eligible Auto Sockets, including existing ones, stops authoring/build.

Native Auto scanning and competing expressions retain control of native outputs.
Reload restores saved Auto/Legacy and starts owned Sockets OFF.
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

## VRCFury compatibility (2026-09-13 amendment)

Exact-version rejection is replaced by installed schema/capability inspection.
The core has no compile-time VRCFury assembly reference. Missing VRCFury/SPS
shows a warning and official VCC update/install guidance and disables generation,
regeneration, apply and test-Plug operations. A malformed required basic schema
also stops before authoring mutation. Other dependency/Unity compilation failures
cannot be repaired by this package's warning UI.

SPS1 (`configureSps` or `enableSps` on Plug without SPS2 tags) shows an upgrade
warning but supports available basic sockets. Missing options are disabled and
identified; generation copies requested settings and omits unsupported features
from the effective saved setup. UI inputs are not modified merely by inspection.
Path, Collapse, custom tangents, Auto, Legacy, Local Only, depth actions,
radius offset, test Plug and attachment API availability are checked separately.
Pre-stops SPS2 retains native Dual Mode; Legacy is unavailable there.
SPS1 has no imposed SPS2 Auto16 cap. Unrelated/native Dual Mode controls remain.

Public API calls are late-bound only in Compatibility with exact public signatures.
Before the public factory era, native basic Socket construction uses required schema
checks and attachment uses public Unity ParentConstraint. Depth auto-configuration
requires the public depth/action API and its matching serialized fields. Missing
radius offset leaves the native default. Missing tangent overrides use native path
interpolation; missing Collapse leaves thickness unchanged and visibly warns.
New local tangent fields convert world distances using the previous/current stop
scales respectively, matching the observed upstream migration.

When native oscId is absent, the existing exact prefix/asset GUID/part token is
stored in the native Socket name. This is visible in its native Inspector and OSC
name; the final menu still restores the configured display name. Upgrading reads
only valid tokens, migrates ownership to oscId when available, and retains external
asset/path validation. Existing Plug names as well as Socket identifiers participate
in build-token collision checks. No custom component is added to generated objects.

Official stable-source boundaries: SPS first appears in 1.171.0; SPS2 including
stops/vector tangents/Collapse in 1.1349.0; local tangent units in 1.1409.0. Beta
versions can have intermediate schemas. These observations guide detection and
are not claims that every release passed Unity/VRChat tests; see VALIDATION.md.

## Experimental finger-ring calibration (unreleased 1.1.0)

This is a local prototype with revision-specific Editor evidence and open VRC PC
and actual-avatar visual gates. Optional IDs
fingerRingLeft/fingerRingRight are separate from palm sockets, default OFF,
and untouched by existing presets. Native Auto stays OFF for these IDs;
legacy/custom Auto behavior and the all-avatar 16-socket guard are unchanged.

A user closes the reference pose using their existing gesture tools, sets the
ring center and +Z forward direction via avatar-local fields/Scene handles,
then explicitly captures two coincident joint-local frames. ThumbDistal and
IndexIntermediate are the initial Humanoid mapping; the user must verify actual
joint positions. No gesture clip is edited. An uncalibrated included ring blocks
generation. Capture stores relative position/rotation offsets and joint paths;
regeneration never recalibrates against the current pose. UI capture is a draft
until Apply; recapture replaces the adjustment frame. Ordinary unchanged Apply
preserves manual pose. Disabling removes generated objects while keeping settings.

A two-source ParentConstraint blends the calibrated frames with thumb weight
[0,1], initial 0.5. This is not the midpoint between anatomical joints: both
frames coincide with the user-selected center at capture for every weight.
There is no Hand-fixed rotation or cross-product normal, and therefore no
collinear-normal fallback. Open hands continue following; no gesture detection
or automatic activation is added. Actual visible behavior remains a validation
question. Missing joints, changed joint paths, invalid values or non-unit world
joint scale fail closed in this prototype. Joint world matrices must also match
their unit rigid rotation frames, so compensated nonuniform ancestor scale cannot
hide shear behind a lossyScale value near one. Non-finite scale and saved UI
center/rotation values are rejected. Scaled rigs need further observation.

Native tracking, generated-avatar SDK conversion and persistence/Auto regression
have exact-revision evidence in VALIDATION.md and the owning Issue. Converted
dynamic tracking has a separate Play Mode test through public SDK conversion and
ApplyConfigurationChanges; its result must be recorded before claiming an SDK
simulation PASS. All Play Mode results use a persistent public TestRunner callback
across domain reload. SDK simulation does not certify the VRChat client.

Ring instructions, fields, actions, status/error messages and standard left/right
names use the existing Japanese/English/Korean/Simplified/Traditional catalog.
An authored menu-name override still wins in every language; stable IDs, pose names
and calibration values are unaffected. Automated translation completeness is not
native-speaker approval. Actual-avatar aesthetics and VRC PC remain human gates.

### Scale boundary

A fixed positive uniform scale of the entire avatar is mathematically different
from nonuniform bone scale: it preserves angles and has one scale factor. A future
extension could potentially capture/rescale the offset consistently, but both
native and converted constraints and later avatar-scale changes require real
observation. The current prototype still rejects non-unit whole-avatar scale.

The current offset is a world-distance vector expressed in joint rotation axes:
`d = inverse(R) * (center - jointPosition)`. If a rig changes uniform scale after
capture, joint positions scale but the saved distance does not; the desired offset
and saved offset then differ. Merely removing the scale guard is insufficient.
Nonuniform scaled/rotated bone chains can additionally introduce shear, for which
one scalar or a quaternion cannot describe the complete frame. This remains
unsupported; no scale-support expansion or avatar transform modification is made.
No distribution format is changed here.


### Issue 180 compact display settings (unreleased 1.1.0)
The UI language uses the system language only when no explicit Editor preference
exists. New setup defaults use the system language independently of that preference.
Japanese, English, Korean and Simplified/Traditional Chinese are supported;
unsupported system languages use English. Existing serialized settings retain
their menu language (missing legacy language remains Japanese).
Menu language lives in an initially collapsed display section. Each included socket
shows its effective menu name beside Depth actions and Rename. Rename reveals the
override field; turning it off clears the override and restores the translated
standard name. Existing overrides remain enabled; IDs, pose paths and Auto behavior
are unchanged. Window draft/expanded state supports serialization and Undo.


## Automatic initial finger-loop calibration amendment (unreleased 1.1.0)

New uncalibrated, included fingerRingLeft/fingerRingRight attempt a bounded
bone-based initial calibration before the authoring transaction. A disposable
Transform-only copy uses the existing Humanoid Avatar through public
HumanPoseHandler. Only the selected thumb/index muscles change: thumb stretch
(-1,-0.75,-0.75), spread-0.75; index stretch(-0.25,-1,-1), spread0. No source
Animator, scripts, constraints, renderer, pose or clip are modified/copied.
The copy and HumanPoseHandler are disposed on success/failure. This reference
pose is not guaranteed to match the user's runtime Gesture.

Seven mapped points (Hand; thumb/index proximal, intermediate, distal) form a
bone contour. Unmapped/distinctness failures, nonfinite values, tiny segments,
non-unit/sheared frames, estimated tip gap >0.2 index length, area <0.08 span²,
plane deviation >0.2 span, exterior centroid or uncertain palm-relative normal reject estimation.
Terminal segments extend by0.6 of the preceding distal segment: this is a bounded
bone approximation, not a finger-surface measurement. The area centroid and
palm-relative plane normal supply center/+Z. Both joint-relative offsets are
captured on the posed copy, then values and original paths are carried back.
Thumb weight is preserved, not inferred from a single closed pose.

Existing saved/manual calibration is never automatically replaced; pending
manual inputs require explicit capture. Automatic retry is explicit in UI.
Generation failure commits no estimate/output. Successful initial calibration
returns to the window draft and settings snapshot. Regeneration with unchanged
ring calibration also retains its pose-local manual position/rotation/scale;
explicit recapture replaces that adjustment frame. Other regeneration semantics
and native Auto exclusion/16 limit stay unchanged. Scaled rigs remain unsupported.
Actual-avatar appearance, actual Gesture match and VRC PC remain human gates.

The menu-language row is always visible in its original top position. Menu display
foldout retains display-name fields; enabling Rename opens it. Stable IDs and
user overrides remain unchanged. Japanese standard labels are 左手の指わっか /
右手の指わっか; English uses finger loop. Historical standard aliases are retained
as migration inputs. Native-language quality still needs human review.


## Real-gesture refinement of the initial loop estimate (unreleased 1.1.0)

The estimated opening contour excludes ThumbProximal, which belongs to the
palm/web rather than the hole. Its normal is the least-variance axis of the
contour covariance, with area, rank/planarity and palm-relative direction guards;
the signed area centroid is projected onto that fitted plane. The existing
bounded reference pose, tip approximation and unsupported-scale guard remain.

New automatic calibration follows ThumbDistal plus IndexProximal, reducing the
reference-to-gesture rotation discrepancy measured on MANUKA. Its chosen joint
is recorded in the existing indexPath field; no settings schema/ID is added.
Saved IndexIntermediate calibrations continue using IndexIntermediate and are
never silently upgraded. Uncalibrated manual capture retains IndexIntermediate;
automatic recalibration is explicit and changes its selected frame deliberately.
Unknown/remapped paths fail validation. Stored weights and default0.5 remain
unchanged: a single avatar does not establish a universal tracking weight.

No user Gesture, Animator controller, Avatar asset or mesh is modified. CPU
mesh projection used during validation is diagnostic evidence only and is not
included in the runtime estimator or package dependency contract. Other-avatar,
visible fit and VRC PC validation remain open.
