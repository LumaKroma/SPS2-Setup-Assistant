# Single-ended paths and inline help icons — 2026-09-13

Baseline fbffd3f84156bc655e469f724364b64c459238f7. Same owned warm session,
Unity 2022.3.22f1 / SDK 3.10.4 / VRCFury 1.1403.0. Evidence and source-host
manifest: `Evidence/virtual-path-20260913`.

- Clone generation for mouth-only and anus-only retains exactly three stops.
  All stop positions and rotations match the both-included reference (reported
  errors 0). Native Socket count matches included parts; all authoring parent
  constraints have valid sources. The missing side is a virtual Transform only.
- Collapse-only apply preserves a manually adjusted stop; penetration OFF
  clears paths, ON restores them. Neither endpoint included clears paths.
  Undo restores the anus-only path, Redo returns to no path.
- Full-preset native mouth-only and MA anus-only SDK preprocessing pass with
  14 owned Sockets, no excluded menu entry and the included entry present.
  Native Hole marker values [1,1,1,0] preserve internal Collapse and normal-width
  exit. Remaining Unity ParentConstraints report no missing sources. This is
  build evidence, not proof of VRC runtime animation/deformation.
- Help layout now bounds the toggle to label width plus checkbox padding,
  places the existing help texture next, then consumes remaining row width.
  The tooltip explains single-ended behavior. Source/layout review, not a
  claimed automated pointer-hover screenshot test.
- EditMode job 59a91807ffe544ef830c8d1881e59732: 21/21 passed, 0 failed/skipped.
  Independent read-only review of both changed implementation files found no
  must-fix references, persistence or layout regression. Runtime following and
  visual GUI appearance were explicitly not observed by the reviewer.
- Console raw capture retained 140 ordinary Debug.Log entries misclassified as
  exceptions by MCP; no actual exceptions. After retaining/clearing these logs,
  the final selected client 35188a0e-45ff-4747-93c1-fb1b33f12c0b was ready with
  no compilation errors and Console0. Original MANUKA window binding restored.

Original avatar is not regenerated or saved automatically. Apply inclusion/
penetration changes or regenerate to adopt the path; existing unchanged manual
paths stay intact. Runtime following of virtual exits during head/hip movement,
SPS deformation and other avatars remain Validation / VRC PC. No upload/release.

# Help icons and Instant authoring default — 2026-09-12

Baseline 49d95e9839194d4f54a7beab95e17982fef231af. Visible native help icons
added only to nonempty tooltips; labels/icons share the same text. Instant
defaults OFF in newly created setup settings, preserving explicitly saved
values through Copy/JSON. Public Editor probe checks the default, persistence
and built-in help texture availability; compile/window readiness and Console
checked in the same owned warm host. Evidence: `Evidence/help-icon-20260912`.
This changes authoring default and presentation only, not the existing Instant
runtime flow. No automated hover visual check or new VRC PC evidence. Original
avatar not regenerated or scene saved. Previous VRC PC gates remain.

# Chest label and setup tooltips — 2026-09-12

Baseline d378bd63a5cf24a6758819733950c2a5be7d7848. UI-only delta: default chest
name 胸, exact former built-in label migration on window load, and four standard
GUIContent hover tooltips. New/old defaults, custom names/custom parts and live
MANUKA window binding checked via public authoring API/serialized window data.
Compilation and final Editor readiness/Console verified in the same owned warm
host. Evidence: `Evidence/tooltips-20260912`. Tooltip text/GUIContent wiring
reviewed against the current behavior contract; no automated pointer-hover
visual test and no redundant runtime suite for this presentation-only change.
Original avatar was not regenerated or scene saved. Apply without replacement
to persist the new menu label. Prior VRC PC limitations remain unchanged.

# Mouth face-profile angle — 2026-09-12

Baseline cd3970a68ce46087fc84d2c3dfb95b6c2e179fe4. Known bounded placement delta,
same owned warm Editor/session; source/host manifest and clone probe results in
`Evidence/mouth-angle-20260912`. MANUKA mouth position remains
(0,1.091941,.071624), outward direction becomes (0,-.551994,.833848), pitch
33.50393 degrees downward. Opposing exit position error 0 and direction dot -1.
Nonreplacement preserves manually adjusted rotation. Side render with a direction
line inspected against the actual local avatar face; image remains local.
EditMode job 0a451253818f4cbc81a270236f1153cf: 21/21 pass, no failed/skipped tests.
No animator/runtime behavior changes or new VRC PC proof. Regenerate to adopt
this angle; the existing user scene was not regenerated or saved automatically.

Preflight scope note: management-root preflight passed identity/revision/project/
credentials but cannot match a separate warm-host lock; host-root preflight
cannot load the management-only dashboard module and reports expected warm-host
residue. These are retained as non-passing tool results. The established
Pragmatic Development split remains: clean independent source, package hash
parity, full Project reread, official active Issue121/PID38260 lock and exact
Coplay project/client selection were checked separately. No clean-release claim
or Editor launch/lock bypass; rollback is the prior source package, and any
ownership/identity conflict stops Editor work. Other avatars and actual SPS
deformation/head motion remain Validation / VRC PC.

# Centered oral curve and optional Collapse — 2026-09-12

Baseline `ae9e21da37c9826fd7d546b5bfa61a769ef87bcb`; exact changed source is pinned
by `Evidence/collapse-option-20260912/validated-source-hashes.json`. Same owned
warm Unity 2022.3.22f1 session, VRC SDK 3.10.4, VRCFury 1.1403.0 and CoplayDev
10.2.0. Pragmatic Development evidence; no release or VRC PC pass.

- Setup child checkbox defaults ON for new settings and older JSON. OFF/ON/OFF
  updates all three native segment flags in both directions, persists to the
  external asset, and preserves manual positions and custom tangents. Undo/Redo
  restores the preference; disabling/re-enabling penetration retains it.
- Current MANUKA oral cubic world points: (0,1.09194,.07162),
  (0,1.10950,.00138), (0,1.11779,.00992), (0,1.04755,-.00413).
  Reverse control-point differences are below 0.00000004 m. The throat point
  is centered between the body intersections at Neck height.
- Full 15-Socket SDK build with native attachment and Collapse OFF passes;
  native material `_SPS_SocketHole` values are [0,0,0,0]. Modular Avatar build
  with Collapse ON passes with [1,1,1,0] in both directions. Both test Plugs,
  nonreplacement pose preservation, menu ordering/no Other group, unsaved
  Local Only default 0 and FX OFF/ON/OFF/ON/OFF with/without Instant pass.
- EditMode job `6d1ff62f668a4681afc2025dfab1f647`: 21/21 passed, 0 failed/skipped.
- Independent read-only review of the final four implementation files found
  no must-fix regression. Native tangents use rotation-relative meters; native
  marker scale .001 is animated to 1 by SpsSocketMarkerProperties. No 1000x
  correction is needed. This conclusion is dependency source inspection,
  not an additional live marker-scale measurement.
- A static tube generated from the native cubic formula was rendered against
  the local avatar as a geometry check, not as SPS/GPU deformation evidence.
  The current body has neck sections about 4.9 cm deep, so a 5 cm plug cannot
  be assumed concealed when Collapse is OFF. The preview still shows exposed
  tube at parts of the neck; no complete concealment claim is made. Avatar
  imagery is kept locally, with its hash recorded rather than redistributed.
- Console evidence retains one native VRCFury Inspector MissingReferenceException
  after destroying a temporary Plug probe, plus 151 normal Debug.Log messages
  misclassified as exceptions by MCP. Final selection/window restored to
  MANUKA_lilToon; after retaining/clearing logs, Editor ready, compilation errors
  false and Console errors 0. Original scene was neither saved nor regenerated.

Remaining: regenerate to adopt the new oral curve, then inspect native SPS
deformation with Collapse ON/OFF in Gesture Manager and VRChat PC, including
head/neck movement and both entry directions. A collapse-only nonreplacement
apply intentionally preserves the current manually edited route. Other avatars,
large plugs and nonuniform inherited scale remain unverified. Keep the task at
Validation / VRC PC and the owned Editor open for manual validation.

# Native path collapse for throat concealment — 2026-09-12 (prior revision)

Baseline6081e75178ff0a8edc49d4bbf73b0df3974807a6, same owned session6.
Evidence/source manifest: `Evidence/throat-20260912`.
User clarified that adjustment belongs to SPS Socket path options. No test Plug
diameter/UI change is included. Native1.1403.0 GuidedPathList exposes Collapse
per segment; native baker writes Hole on the previous marker, and installed
`sps_deform_curve.cginc` sets internal radius multiplier0 for that segment.
Terminal RingOneWay retains external width. No shader or dependency edit.

- First upper waypoint moved from Chest(.89915m high) to Neck(1.04755m high)
  on MANUKA; Neck waypoint distance0, both opposing exit position errors0.
- Native and MA full SDK preprocessing pass. Both authored paths have shrink
  `[true,true,true]`; actual baked shader-property animations are Hole
  `[1,1,1,0]` for entrance/three stops in both directions. The final0 prevents
  terminal collapse beyond the exit. This is build/graph evidence, not a render.
- Penetration OFF clears both arrays; ON restores collapsed paths. Undo returns
  OFF, Redo returns ON. Unchanged nonreplacement preserves hand-adjusted stops.
- Test capsule remains .05m wide/1.5m long. Native mouth OFF/ON/OFF/ON/OFF with
  Instant present/removed still passes. Menu structure/Local Only unchanged.
- Independent read-only review found no required correction in the two source
  files; native collapse and preserved exits matched the observed SDK output.

All21 EditMode tests pass. A native Plug inspector callback accessed a destroyed
temporary fixture; its MissingReferenceException stack is retained. Final target
restoration and Console audit distinguish teardown from product compilation.

Actual throat concealment in Gesture Manager/VRChat, other avatars and inherited
runtime/visual acceptance gates remain open. User scene is not regenerated or
saved; regenerate to adopt Neck/Collapse settings. Editor is left open for that
manual check. No push, upload, merge or release.

# Authoring recovery and penetration follow-up — 2026-09-12

Baseline2511eb4063129e0318c177ebbeebe24aff66d2c3, same session6/environment.
`Evidence/followup-20260912` pins source and observations separately.

- Observed the user's window descriptor null while MANUKA and its generated
  root remained loaded in Edit mode. Persisted GlobalObjectId now restores the
  same avatar after deliberate reference loss. Unresolvable IDs are explained;
  explicit re-detect clears that stale ID and checks selection/unique ownership.
- Anus underside ray moved posteriorly, with surface clearance. On MANUKA its
  local position changed from (0,.67488,.01816) to (0,.68295,-.02368). A failed
  diagonal-ray experiment and the subsequent actual surface scan are retained.
- Both native path exits match the opposite Socket position (error0), with
  reversed +Z (dot-1). Intermediate frames follow route direction, separately
  for each direction, instead of copying bone orientations.
- Long capsule:3729 vertices,1.5m x .05m, persistent mesh/material subassets,
  tip .08000008m in front of mouth; independent standard/long Plug reuse,
  regeneration retention, Undo/Redo and saved Prefab mesh restoration pass.
  Long Plug has zero package-defined components. Side render confirms its
  simple capsule appearance/initial mouth placement, not runtime deformation.
- Native and MA SDK preprocessing pass with both Plugs. Capsule renderer remains
  in built output. No その他 group; direct sockets follow the specified first
  seven and paginate. Mouth FX OFF/ON/OFF/ON/OFF passes with/without Instant.
- All21 existing EditMode tests pass; source blendshapes remain unchanged.
  Translated/yaw90/scaled1.2 geometry remains within .000003341m and0degrees.
- Independent read-only review found the stale-ID explicit recovery issue;
  explicit re-detect now clears it. No other concrete blocker was reported.

A Unity UIElements InspectorElement NullReferenceException occurred during temporary
selection/fixture teardown; its engine-only stack is retained. After restoring
the user target and clearing preserved logs, final Editor is ready/Console0.

User avatar/scene were not regenerated or saved. Regenerate to adopt automatic
anal placement and path frames, then summon the long Plug. Existing hand edits
survive nonreplacement. Actual deformation, GM Contact/driver behavior, subjective
placement across avatars and exact-commit VRC PC remain unverified. No push,
upload, merge or release. Editor stays open for the requested manual checks.

# Menu and placement delta — 2026-09-12

Baseline `0eb74bec80d6ba562855a9987d807f8782c0217d`; same owned session 6,
Unity 2022.3.22f1 / SDK 3.10.4 / VRCFury 1.1403.0 / CoplayDev 10.2.0.
The source and evidence manifests in `Evidence/menu-placement-20260912` pin
this delta separately from earlier evidence. This is development validation.

- Observed native SDK and optional MA preprocessing with 15 generated sockets:
  one SPS2 root; page 1 `設定|口|胸の間|膣|肛門|右手|左手|次へ`, page 2
  `両手|その他`. Shared settings are nested; existing native options/socket remain.
- Local Only checked: native Stealth control appears under Settings, parameter
  initial 0/unsaved. Unchecked (MA probe): both Local Only and old Stealth menu
  controls absent; native parameter still 0/unsaved. No new runtime graph.
- Actual generated FX evaluation with Instant present/removed follows mouth
  OFF/ON/OFF/ON/OFF. Native networking/haptics remain VRC/GM acceptance gates.
- Actual MANUKA skin weights distinguish face `Body` (only Head coverage) from
  `Manuka_body`. Unmapped Humanoid Toes and active `Shrink_stocking` explain the
  failed foot queries. Temporary measurements exclude Shrink shapes, preserve
  heel pose, and leave every original blendshape weight unchanged.
- New poses place chest ring horizontally; palm/sole/cleavage use native radius
  offset along outward +Y. Pelvic origins use underside surface rays; nipple
  origins use breast surface. Fifteen poses and direction vectors were captured.
- The first translated/yaw90/scaled1.2 probe exposed a missing mesh-scale
  transform (up to 0.2013 m error). After correction, inverse-avatar position
  differences are <= 0.000003341 m and quaternion angle differences are 0.
- All 21 existing EditMode tests pass (0 failed/skipped). They cover existing
  package contracts; new placement/menu observations use real disposable probes.
- Front/side clothed MANUKA renders were inspected locally. Occluded pelvic and
  cleavage regions cannot receive visual acceptance from these renders. Other
  avatars, hand poses, shoes/clothing fit and subjective quality remain unverified.

The user scene and its existing 12-socket root were not regenerated or saved.
The updated setup window is bound to MANUKA_lilToon with applied settings loaded.
Use regeneration to adopt new automatic poses/radius offsets; nonreplacement
preserves manual edits. The owned Editor remains open at the user's request.
A native Plug inspector callback logged MissingReferenceException after a
transient probe Plug was destroyed; its stack was retained. This is probe
teardown evidence, not a runtime pass. Normal native progress logs were also
misclassified as Exception by MCP; they were identified by Debug.Log stacks.

No upload/push/merge/release. Exact-commit VRC PC and the prior visual/runtime
acceptance gates remain open; do not infer those passes from Editor results.

# User-requested repair validation — 2026-09-12

Repair baseline: `71d891c6cedf44066dfbac6d1ff3fd420d127ef2`.
Pragmatic Development in the same pinned Unity/SDK/VRCFury environment below.
The source manifest in `Evidence/repair-20260912` identifies the repair source;
the earlier section and evidence describe the previous implementation only.

- 21 EditMode tests passed again, zero failed/skipped.
- New 15-Socket generation has root name SPS2 and zero custom root components.
- External settings reload, preserved edited poses, separate duplicate settings,
  Undo/Redo and Prefab restoration passed. Changing an instance after saving a
  Prefab left the saved Prefab's settings intact. Missing settings asset stopped
  lookup. Legacy metadata migrated through Show Test Plug with its Plug retained.
- Actual native and MA SDK preprocessing passed. The menu root is one SPS2 entry;
  Auto and Legacy are not duplicated, and existing Socket/native options remain.
- Actual generated FX evaluation with and without Instant followed OFF, ON, OFF,
  ON, OFF for the mouth's BakedSpsSocket. The earlier empty WD OFF Instant clip
  reproduced latching ON. The repaired policy restores OFF. This is Animator
  evaluation, not direct Gesture Manager or VRC-client acceptance.
- Plug reuse retained the same object and took 0–1 ms in the latest probes;
  first generation took 93–130 ms. Earlier repeated unconditional metadata saves
  took roughly 1.8–2.2 seconds. Measurements are local observations, not budgets.
- Independent read-only review found and resolved scene-discard snapshot and
  legacy Plug migration issues. No further concrete blocker was reported.

Final Console audit also found an existing polymorphic depth-action copy error:
`CopyFromSerializedProperty` could not initialize the managed-reference array.
The guarded adapter now copies group fields and managed-reference action values.
A real probe verified BlendShape renderer, clip wrapper reference, object target,
TurnOff and a 3-to-1 action update, with zero Console errors after clearing and
rerunning. Native/MA SDK, OFF cycling and all 21 EditMode tests passed again;
the `*-final.json` evidence and current source manifest cover this correction.

The empty WD OFF source-controller fixture stalled in a native VRCFury dialog
before this repair's final checks; that owned session was aborted, not passed.
WD OFF source-avatar compatibility, visual placement across three avatars,
Gesture Manager Contact/driver effects and exact-commit VRC PC remain unverified.
No upload, release, or merge was performed. Existing handoff gates remain open.

# Previous implementation evidence (before the repair)

# Issue 121 development validation

Development comparison: baseline
`94596332f0245e131cbb83b092076835f691719f` on `issue/121-private-poc`
to the approved `0.2.0-dev.1` delta. This is not a formal release.
The [validated source manifest](Evidence/issue121/validated-source-hashes.json) pins all compiled source, assembly definitions, the package manifest and display assets; every hash matched the package in the validation host. The PR records the containing commit. `sha256` records observed source/host bytes; `repositoryLfSha256` records the repository LF form. Evidence text is stored with LF line endings before hashing.

## Observed Editor results

Owned isolated project, Unity `2022.3.22f1`, SDK `3.10.4`,
VRCFury `1.1403.0`, CoplayDev MCP `10.2.0`.

- 21 EditMode tests passed, zero failed/skipped: existing public attachment,
  legacy planner/ownership and Undo-factory tests plus settings preservation,
  nonlinear range and foreign-metadata boundary tests.
- Actual SDK preprocessing passed for a MANUKA-based isolated fixture with
  15 generated Sockets, two three-stop paths, an existing native Socket and an
  IcePop test Plug.
- MA preprocessing also passed with native Save Sockets enabled. The existing
  individual Socket remained saved; all owned Sockets were unsaved/default 0.
  Auto was saved/default 0 and Legacy saved/default 1.
- The metadata component was removed and the test-Plug object retained.
  The generated FX controller, Instant state machine/clip and SDK driver were
  present in NDMF saved asset output.
- Actual Animator evaluation followed Ready → DeniedHeld on a denied press;
  disabling Legacy while held stayed DeniedHeld. Release returned to Ready.
  Two subsequent press/release cycles reached Held and returned to Ready.
  This evaluates the graph; it does not emulate the SDK parameter driver's
  in-client effects.
- Authoring checks passed for generation Undo/Redo, preserved nonreplacement
  identity/manual pose, custom-target world-pose preservation, regeneration
  identity/single-Plug retention and restored automatic pose. Saving/reloading
  a Prefab retained metadata, custom Transform and renderer references.

The SDK fixture clones a licensed avatar available in the private validation
host, removes unrelated behaviors and uses isolated consistent Write Defaults
controllers. It does not establish full compatibility with that avatar's
original expression/animation stack.

## Failures retained during development

An initial mixed Write Defaults probe and a later SDK probe with connected
Prefab instances stalled in native dialogs. Each owned Editor was recovered
under the repository's existing shutdown authorization and those sessions
remain aborted. A subsequent correctly prepared isolated fixture passed.

Undo/Redo initially lost metadata; this was corrected and rechecked. Unity's
Editor JSON path also dropped object references in an isolated observation;
the final short-lived authoring snapshot uses normal JsonUtility, with actual
Prefab reload and reference checks. Failed probes are not counted as passes.

## Outstanding acceptance gates

- Current UI visual review, including narrow layout and generated controls.
- At least three materially different Humanoid avatars: placement, orientation,
  midpoint motion and extreme poses, judged visually.
- Gesture Manager/native Contact depth behavior, self interaction, loss,
  reacquire, disable, competing expressions and SDK driver effects.
- Exact-commit VRC PC validation. No upload was performed or authorized.
- Fresh VCC install/remove and any dependency combination not observed above.

These gates must remain visible in the Issue/Project. A passing Editor build
or graph evaluation is not a VRC runtime compatibility guarantee.

## Reproduction and retained evidence

[Evidence hashes](Evidence/issue121/evidence-hashes.json) cover the native and
MA build results, authoring results, EditMode report and the three disposable
probe sources. Run these probes only inside an owned isolated validation
session with the stated dependencies and a separately licensed MANUKA fixture.
They intentionally create temporary SDK/NDMF output; they are not package
runtime code or a distributable avatar fixture.

A final independent read-only review of the Editor/Runtime source found no new
blocking defect. It checked metadata ownership, nonreplacement/regeneration,
Instant re-entry, native parameter correlation, MA movement and build asset
persistence against the observed results. This is code review evidence, not a
substitute for the outstanding acceptance gates.

The current UI window opened through MCP without compilation errors. Both
normal and utility-window capture attempts returned white Unity content, so
visual acceptance remains unverified. Gesture Manager's actual status reports
its MCP bridge unavailable: installed CoplayDev 10.2.0 differs from the bridge's
verified 10.1.2. Its guard was preserved; no runtime smoke-test pass is claimed.
Both final owned Editor sessions exited normally, with bridge shutdown,
preferences restored and the official lock released.
