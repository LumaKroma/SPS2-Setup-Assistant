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
