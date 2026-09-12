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