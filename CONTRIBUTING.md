# Issue 121 capability compatibility amendment (2026-09-13)

The user's compatibility expansion and explicit SPS1-basic-generation answer
supersede the exact-1.1403.0 guard below. Native schema access remains isolated
in Editor/Compatibility. Public API late binding with exact public signatures
is permitted there so pre-API/absent versions still compile and show guidance.
Native basic Socket creation and public Unity ParentConstraint fallback are
allowed when public factories are absent. Do not patch/vendor dependency code.
SPS absence blocks generation; SPS1 warns and allows supported basics; missing
features warn and are omitted from effective generated settings. Current schema
recognition is not an all-version runtime certification. Update output contract,
validation and package checks together; other-avatar/VRC PC gates remain open.
# Active full-version scope

Issue #121 comment 5644512003 and the approved exception in AGENTS.md override
the 0.1.0-only source and output restrictions below. Unknown compatibility
behavior requires a disposable real Unity observation and independent review
before promotion. The package version is private development 0.2.0-dev.1; no
release claim follows from expanding this implementation.

# Contributing

## Intake

Implementation work requires a GitHub Issue that records the goal, non-goals,
exact baseline, allowed paths, public API sources, invariants, acceptance
criteria, validation gates and stop conditions. Add a `Context Check` comment
before the first source write.

Branch names use `issue/<number>-<short-description>`. Do not push directly to
`main`. Open a Draft PR early and use `Refs #<issue>` until every manual gate
is complete.

## Source boundary

The 0.1.0 package may contain only Editor C# source, Editor tests,
documentation and package metadata under
`Packages/com.lumakroma.sps2-setup-assistant/`.

Allowed dependency surfaces:

- `com.vrcfury.api.FuryComponents` and its public wrappers;
- `nadena.dev.modular_avatar.core.ModularAvatarBoneProxy` and its public
  fields/enums;
- documented VRChat SDK and Unity Editor APIs.

Modular Avatar is optional. Its public integration must remain isolated in a
version-defined Editor assembly. The core Editor assembly and VPM dependency
manifest must continue to compile/install without MA.

Do not use reflection, dependency private/internal types, dependency serialized
field names, source copying, vendoring or patching. Do not modify avatar FBX,
source model/prefab, Animator Controllers, Expressions Menu, materials,
dependency packages or backend-generated build output.

## Generated-output contract

- The tool creates one exact-name root directly under the chosen avatar.
- Each selected location owns an independent bone anchor and Socket pose.
- Generated transforms are regular editable GameObjects.
- Re-run may replace only the exact tool-owned root and must fail closed if its
  ownership is ambiguous.
- One Undo group reverses the complete operation.
- Removing the generated root fully uninstalls the result.

## Validation

Before requesting review:

1. run `pwsh -File Tools/Validate-Package.ps1`;
2. run `git diff --check`;
3. inspect the complete diff and verify the worktree is clean after commit;
4. record what remains unverified.

Unity compilation, EditMode tests, Undo/Redo, Prefab Stage behavior, native
VRCFury serialization equivalence, placement on three materially different
Humanoids, fresh VCC install/removal, Gesture Manager and VRC PC behavior are
separate exact-head gates. Do not claim them from static inspection.

## Protected actions

Explicit user approval is required to merge, release, publish a VPM listing,
make this repository public, install/upgrade packages, or start Unity/VRC for a
manual gate.
