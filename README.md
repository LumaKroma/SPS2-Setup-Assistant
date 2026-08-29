# SPS2 Setup Assistant

SPS2 Setup Assistant is an Editor-only helper for adding a practical set of
VRCFury SPS2 Sockets to a Humanoid VRChat avatar. Select a
`VRCAvatarDescriptor`, choose the locations and attachment profile, then run
one setup command.

This repository is currently a **private 0.1.0 proof of concept**. It is not a
published VPM listing or a runtime-compatibility claim.

## Output

The assistant creates one removable root directly beneath the selected avatar.
Each enabled location gets its own bone anchor and editable Socket pose:

```text
SPS2 Socket Setup [com.lumakroma.sps2-setup-assistant]
├─ 01 Head Mouth
│  └─ Socket Pose
└─ ...
```

Each numbered location node is the attachment system's `Bone Anchor`; its
child is the editable `Socket Pose`.

Two attachment profiles are available:

- **VRCFury** — public VRCFury Armature Link plus public VRCFury Socket API.
- **VRCFury + Modular Avatar** — public MA Bone Proxy plus public VRCFury
  Socket API.

The generated local `+Z` direction is the Socket entry direction. Transforms
remain ordinary editable GameObjects. Re-running replaces only the exact,
tool-owned setup root; unrelated Sockets are never inspected or modified.

## Scope and safety

- Socket-only and Humanoid-only.
- No Plug setup, runtime scripts, Animator/menu/material changes, reflection,
  private serialized fields, build preprocessors, or generated-output edits.
- All Socket fields stay at VRCFury-native defaults except the public name and
  explicit `Auto` mode. Advanced options remain owned by the VRCFury Inspector.
- One Undo group owns the whole setup transaction.
- Initial placement is a deterministic first placement, not a fit guarantee.

See [the output contract](Packages/com.lumakroma.sps2-setup-assistant/Documentation~/OUTPUT_CONTRACT.md)
for the exact hierarchy and current heuristic boundary.

## Dependencies

The private PoC targets Unity `2022.3` and declares these VPM dependency floors:

- VRChat Avatars SDK `3.10.4`
- VRCFury `1.1401.0`
- Modular Avatar `1.18.1`

The floors and latest stable versions still require the Issue #121 Unity
compile matrix before any release claim.

## Validation status

Static package/source checks and EditMode test sources are included. Unity,
fresh VCC installation, native VRCFury serialization comparison, three-avatar
placement review, Gesture Manager and VRC PC checks remain exact-commit gates.

## License and affiliation

Original code and documentation in this repository are MIT licensed. VRCFury,
Modular Avatar and the VRChat SDK are separate dependencies and are not copied
or redistributed here.

SPS2 Setup Assistant is an unofficial tool and is not affiliated with or
supported by VRCFury, Modular Avatar, or VRChat.
