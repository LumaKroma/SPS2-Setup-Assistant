# Private 0.1.0 output contract

## Identity and ownership

The one owned avatar child is named exactly:

`SPS2 Socket Setup [com.lumakroma.sps2-setup-assistant]`

The generator considers a root replaceable only when it is a direct child of
the selected `VRCAvatarDescriptor` and its hierarchy matches the deterministic
catalog signature. Zero roots means a fresh setup. More than one exact-name
root, or one malformed exact-name root, fails closed without mutation.

## Catalog

1. Head / mouth (`Jaw`, fallback `Head`)
2. Chest (`UpperChest`, fallback `Chest`)
3. Hips front (`Hips`)
4. Hips back (`Hips`)
5. Left hand (`LeftHand`)
6. Right hand (`RightHand`)
7. Left foot (`LeftToes`, fallback `LeftFoot`)
8. Right foot (`RightToes`, fallback `RightFoot`)

Missing bones disable only their own catalog entry.

## Placement boundary

The planner derives a body basis from hips/head and left/right limbs, aligned
to the avatar root's forward direction. Offsets scale with measured Humanoid
height. Local `+Z` points from the estimated surface toward the body/bone.

These values are deterministic first-placement heuristics. They are not a fit,
mesh-surface, anatomical or runtime guarantee. The exact-head manual gate must
review at least three materially different Humanoid avatars. Users may edit the
ordinary `Socket Pose` Transform afterward.

## Backend output

- Each numbered direct child of the owned root is the `Bone Anchor`; its sole
  direct child is `Socket Pose`.
- **VRCFury:** `Bone Anchor` receives a public Armature Link targeting the
  chosen `HumanBodyBones`; `Socket Pose` receives a public VRCFury Socket.
- **VRCFury + Modular Avatar:** `Bone Anchor` receives a public MA Bone Proxy in
  `AsChildAtRoot`; `Socket Pose` receives a public VRCFury Socket.

The Socket uses VRCFury `Auto` mode and its other native defaults. Radius
Offset, tags, Guided Path, depth actions and legacy options remain untouched
until an explicit, API-backed product decision authorizes them.
