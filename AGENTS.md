# 1.0.0 publication approval (2026-09-13)

The owner approved the current implementation as 1.0.0 and authorized publishing
this repository, GitHub Release and VPM listing. This supersedes earlier private
development-only version restrictions. Future releases still require approval.

# Unpublished PoC removal (approved 2026-09-13)

The user confirms no old assistant version was public. Remove the PoC window,
generator, catalog/planner and component metadata migration. Current settings
assets and native SPS1/Legacy Compatibility remain supported. This supersedes
older PoC/migration retention requirements below.

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
# Issue 121 full-version exception (approved 2026-09-12)

The active Issue body and comment 5644512003 supersede conflicting PoC-only
rules below. Use Project canonical fields, not the historical ready-for-agent
label. Source baseline: 94596332f0245e131cbb83b092076835f691719f.

- VRCFury 1.1403.0 internal serialization and generated SPS menu/parameter
  integration are permitted only in Editor/Compatibility, guarded by package
  version and required schema. Prefer its public API wherever available.
- Public SDK build callbacks may adjust the generated avatar clone only.
  Never patch/copy dependency code or modify source avatar assets.
- User-approved repair: new generated objects contain no package component.
  Editor settings snapshots use separate assets identified by native Socket IDs.
  Retain old data-only IEditorOnly metadata solely to migrate existing setups.
- Tool-owned menus, controllers, clips, prefab/configuration and approved
  IcePop display assets are permitted. The optional MA backend stays isolated.
- The test Plug is included in uploaded avatars when present. Do not strip
  its GameObject. Removing the setup root removes the entire tool setup.
- Keep this exception, package validation and product contract synchronized.

# Repository agent contract

Follow `CONTRIBUTING.md` for every change.

- One GitHub Issue owns one branch, one working tree and one writing agent.
- Begin source work only from an Issue with `ready-for-agent` and an exact
  baseline. Record a Context Check before the first implementation write.
- Use only public APIs from VRCFury, Modular Avatar, the VRChat SDK and Unity.
  Reflection, private/internal dependency types, serialized-field-name
  mutation and copied dependency code/assets are prohibited.
- Keep the package Editor-only. Do not add runtime scripts, Animator/menu
  generation, build preprocessors, or dependency-generated output.
- Never publish, release, make the repository public, publish a VPM listing,
  upgrade dependencies, merge, or delete protected history without explicit
  user approval.
- Unity and VRC evidence is exact-revision evidence. Static or EditMode results
  must not be presented as VRC runtime compatibility.
