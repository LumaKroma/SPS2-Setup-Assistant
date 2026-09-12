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
