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
