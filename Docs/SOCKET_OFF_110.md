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

Validation must include the regenerated actual14-socket GM ON/OFF/ON/OFF sequence,
related native mode/reset behavior and preservation of authoring inputs. Numeric
GM success does not replace exact-artifact VRChat PC acceptance. The source remains
unpublished1.1.0; there is no release, migration deletion or Auto-policy change.
