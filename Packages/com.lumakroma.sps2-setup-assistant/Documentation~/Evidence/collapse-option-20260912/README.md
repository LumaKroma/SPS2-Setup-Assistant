# Collapse option and centered oral curve

Issue 121; baseline ae9e21da37c9826fd7d546b5bfa61a769ef87bcb.
Management Runbook revision 63c43b9ac6c0a36d762cde9a13e5517eb6319bfc.
Pragmatic Development, owned warm session a4e56932c2bc4c9989754e23652cf773,
Editor PID 38260, lock 5aa13e31d35544e8817b601e03a5f317,
project b5511850106d7df9, final client f8b5ec3e-b2f1-4aa6-8dde-a43ec2b53c23.
Unity 2022.3.22f1 / SDK 3.10.4 / VRCFury 1.1403.0 / CoplayDev 10.2.0.

The adjacent probes are disposable Editor code run only on clones through the
official owned MCP session. They do not change or save the user's MANUKA scene.
Source/host manifest covers the same 100 package runtime/editor/assets/meta
files as the prior revision. Text comparisons normalize CRLF to LF and trim
trailing whitespace; binary files require identical byte hashes. Documentation
and evidence are not part of the compiled source manifest.

Observed: default/missing-field Collapse ON; both directions' native flags;
manual pose/tangent preservation on collapse-only apply; Undo/Redo and parent
toggle persistence; mirrored oral controls; native OFF and MA ON SDK output;
menus, Local Only and repeated FX enable/disable checks; 21 EditMode tests PASS.
The SDK material flags prove setup/build configuration, not visible deformation.

Independent read-only reviewer `/root/api_review` inspected the final four
implementation files and found no must-fix regressions. It confirmed the
forward*0.16d control adjustment is shared by both directions. Dependency source
inspection traced native tangent units through SpsConfigurer scale curves and
BakeHapticSocketsService's always-on SpsSocketMarkerProperties DirectTree:
initial marker scale .001 becomes 1, matching rotation-relative meter offsets.
This was source inspection, not a separate live scale evaluation.

The static tube preview is only a geometry aid using the native cubic formula;
it still exposes parts of a 5 cm tube at the thin neck. Local licensed avatar
image bytes are excluded. Neck profile intersections and the preview image
hash are retained. Complete concealment with Collapse OFF is not established.

Console raw capture remains in the task scratch directory. Of 152 reported
entries, 151 begin with ordinary UnityEngine.Debug.Log and were misclassified
as exceptions by MCP. The remaining VRCFury Inspector MissingReferenceException
followed destruction of a temporary Plug and is retained with its full stack.
After restoring the original avatar/window and retaining/clearing logs, the
final snapshot is ready with no compilation errors and zero Console errors.
Cleanup is not runtime validation.

Remaining: user regeneration to adopt the new curve, native SPS render in
Gesture Manager/VRChat PC, both directions, ON/OFF, and head/neck motion.
Other avatars, large plugs and nonuniform inherited scales are unverified.
Issue remains Validation / VRC PC. No upload, publication or release.
