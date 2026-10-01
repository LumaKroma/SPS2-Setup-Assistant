# 1.1.0 owned Socket OFF restoration

This amends the native-toggle implementation in Documentation~/OUTPUT_CONTRACT.md.
The behavioral contract remains: owned sockets start OFF/unsaved and follow their
actual native toggle parameter. Auto starts OFF/saved, Legacy ON/saved and Stealth
OFF/unsaved. Authoring inclusion/presets and Auto eligibility do not change.

Native VRCFury1.1403.0 represents socket activation as an ON-only clip weighted by
a float in a Direct Blend Tree. In the observed mixed-WD MANUKA/AHGV3 FX, returning
the weight to zero retains the previous active value. Real GM reproduced this on
all14 sockets, twice. Correct paths and OFF parameter values were confirmed.
An isolated copy with WD-OFF layers omitted restored OFF; keeping those layers and
adding explicit owned OFF branches also restored OFF. Neither diagnostic changes
the user's avatar or its WD policy.

After the native build, each owned native Direct branch is wrapped in a 1D tree:
parameter0 explicitly writes the native build-time baseline, parameter1 plays the
same native ON clip. The Direct parent uses one local constant float(default1),
not an expression parameter. This adds no synced memory, timer, transition,
parameter writer, or policy for Auto/Legacy/Stealth. Repeated values, native Auto
selection, exclusivity, reset and initialization use the same existing native
parameters. Native ON-clip defaults, including any float animation parameters,
are captured explicitly. Input state machines/trees are copied, not overwritten.

No avatar-wide WD conversion, VRCFury preference, unrelated control, hierarchy ID,
menu name/parameter, custom saved setting, or other native socket is changed.
Unknown graph shapes, missing/duplicate owned branches, invalid/default-active
root bindings and internal constant-name collisions stop the build. The supported
observed shape is a static single-state Direct graph; other dependency shapes
require captured native validation before support is claimed.

The native Legacy and Stealth empty restoration branches have the same observed
mixed-WD failure. Their descendant bindings do not overlap the14 owned root keys;
Legacy and Stealth share the Light bindings with each other. Restore owned native
non-Light baselines explicitly when Stealth is OFF. When Legacy is ON, restore
owned Light baselines only while Stealth is OFF; otherwise keep them disabled.
Legacy OFF retains its native disable clip. Thus the existing suppression rules
remain: Light = native baseline AND Legacy AND NOT Stealth; other Stealth outputs
= native baseline AND NOT Stealth. Root OFF still hides all descendants. Read
native baseline values, including0, rather than assuming every child starts ON.

Preserve native threshold0/epsilon, layer order/weights/WD, original disabling
clips, existing foreign-socket curves and parameter drivers. Only copied empty
branches receive owned missing baselines. No state/transition/timer/parameter is
added. Every controlled value is defined on entry/reentry and repeated changes;
simultaneous modes, reset/reload, root off/on and native Auto-driven root changes
follow the same current parameters. Unsupported comparison/motion/state-machine
shapes or unreached replacements fail closed. Validation must cover all mode
combinations, repeated cycles and native input/foreign-path preservation, as well
as the original14-socket OFF regression.

Validation must include the regenerated actual14-socket GM ON/OFF/ON/OFF sequence,
related native mode/reset behavior and preservation of authoring inputs. Numeric
GM success does not replace exact-artifact VRChat PC acceptance. The source remains
unpublished1.1.0; there is no release, migration deletion or Auto-policy change.
