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
