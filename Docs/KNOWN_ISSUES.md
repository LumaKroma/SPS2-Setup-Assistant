# Current limitations and validation scope - 2026-11-11

## Interim SPS2 release, not formal SPS3 support

Version 1.2.0 retains beta.5 product code. No compatibility fix or all-version
certification is included. The completed installer and local SDK build checks
are described in VPM_VALIDATION.md; they do not certify VRChat runtime.

## Fury Legacy OFF

With VRCFury 1.1452.0, Legacy OFF writes useLights=false, but the observed native
result retains a Legacy menu/default of 1 and 22 legacy lights. Auto OFF worked
in that observation. A successful local build with VRCFury 1.1454.0 does not
establish that this Legacy OFF limitation is fixed.

## Fury-to-NDMF conversion can stop on depth actions

A conversion of existing Fury settings stopped before the SDK build because
depthActions enableOthers was true while the expected value was false.
This was not established to be user-edited data. Do not infer successful
conversion from the separate successful new-default NDMF generation/build.
Installing the NDMF family does not automatically migrate existing Fury setups.

## SPS for NDMF 1.1436.0 and duplicate names

The NDMF Installer pins SPS for NDMF 1.1436.0. This version provides the SDK
3.10.6-compatible API path; the earlier 1.1430.0 TypeCost call becomes a CS0618
compilation error under its warning-as-error policy with SDK 3.10.6.
No warning suppression or dependency source patch is included.

Duplicate names change from Name / Name1 / Name2 to Name / Name_2 / Name_3.
The updated dependency passed duplicate Socket and Plug preprocessing checks.
Generated menu, OSC and haptics parameter names also change. External mappings
and saved Socket toggle state may not carry over; review them after updating.
VRChat saved-toggle behavior and external OSC/haptics integrations were not run.

Representative existing NDMF settings retained Object ON/OFF, BlendShape and
Clip references, manually adjusted poses and the oral boundary through update,
Apply and save/reload. This is not a guarantee for every saved configuration.
The older duplicate-Plug run failed to load a material; the updated run also
added lilToon and changed MA/NDMF, so it does not isolate a fix in SPSNDMF alone.

## Dependency resolution and remaining limits

includePrerelease=true remains enabled. The NDMF tests resolved Modular Avatar
1.19.0-rc.0 and NDMF 1.15.0-beta.5, not an all-stable dependency stack.
Fury tests resolved 1.1453.0 for fresh installation and 1.1454.0 for updates.
These are observed versions, not promises about future dependency resolution.
The NDMF real-avatar build used a newly generated setup with VRCFury also
installed; it does not establish an NDMF-only-package build or Fury migration.
VRChat runtime, authored Gestures and saved toggle behavior remain untested.

## SPS2-NDMF-JSON-REF-20261017: historical two-test failure, superseded test premise

The October record below is retained verbatim as history, not a current assertion
that beta.5 has two failing tests. Official beta.5 explicitly verifies the null
scene-reference negative case with EditorJsonUtility and the same-session
reference-preserving positive case with JsonUtility. This does not by itself
prove saved-scene persistence or package-update safety.

The November 2 evidence for implementation 45041bdcee390de7a2e10c655f3d39ebfd5d0197
records 228 passed / 0 failed / 14 backend-specific skips per environment across
Fury 1.1403.0, native SPSNDMF 1.1430.0/1.1434.0/1.1436.0, and Stub/Migrator 1.1436.0.
Dedicated save/reload, Undo and local SDK builds were separately exercised.
Native 1.1434 used an earlier test revision with identical production code; the
exact-revision distinction remains part of that evidence.
This disposition does not invent or close a GitHub Issue and does not certify
every Object-action setup, real installer update or VRChat runtime.

## Original October observation (historical)

<!-- Historical record below; not current release readiness. -->

# Known unresolved issues

## SPS2-NDMF-JSON-REF-20261017 — Object-reference JSON roundtrip

Status: **open, not fixed by the finger-ring warning change**. This file is the
local tracking destination; no GitHub Issue has been created for it.

- Cases: `SpsBackendTests.NativeNdmfObjectActionRoundTripRetainsExplicitMode(False)`
  and `(True)`, both failing at `Tests/Editor/SpsBackendTests.cs:115`.
- The test creates an Object action on an installed native NDMF Socket, copies the
  component with `EditorJsonUtility.ToJson` / `FromJsonOverwrite`, and compares
  its target reference. Both cases expect `Depth source` and receive null.
- Candidate package regression: 206 passed / 2 failed / 0 skipped. Replacing the
  five candidate C# files in the same validation project with exact baseline
  `c3d25d24b1bdc3f306df68e74e8d6d49365f6d8e` reproduces the same two failures at
  the same assertion. Candidate files were subsequently restored and hash-checked.
- Existing XML identities: full run
  `6cfba4e224a9d037137568268bdc2cffc8e2ab44e374ea128b83ec8a5d7a476e`;
  baseline two-case run
  `c2b8f1a2f91704b50640eb239d34358b9c862724a21107e48208bcdbb17e731b`.

The failure reproduces without the warning change. This does not identify its
root cause as a dependency defect, a test-method limitation or a user-data bug.
The finger-ring generator and FullSetupGenerator are not called by these cases.
The relevant test and NDMF adapter are unchanged by the candidate.

Before public release, check a representative Object-action configuration through
actual save/reload and package update, then resolve the issue or record a separately
reviewed scope/disposition. Keep the failing results visible. Do not exclude the
cases, weaken the assertion or treat baseline reproduction as an all-pass result.
The candidate README/release notes disclose the unresolved scope. Publication and
any acceptance of that scope require a separate decision.
