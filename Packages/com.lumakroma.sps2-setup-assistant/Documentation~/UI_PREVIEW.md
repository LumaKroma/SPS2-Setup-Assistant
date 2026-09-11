# Issue #121 setup-screen preview

This EditorWindow is a visual review slice of the approved full-version
requirements in [Issue #121](https://github.com/LumaKroma/LumaGimmics/issues/121).
The Issue remains authoritative for unresolved runtime decisions.

Open `Tools > LumaKroma > SPS2 Setup Assistant`. Choose an avatar descriptor,
select a profile, and review the Socket, Path, and In-game Menu tabs.

- Profiles change inclusion only. Existing pose/depth settings and custom
  entries are retained; individual inclusion changes clear the profile highlight.
- Mouth depth is enabled by default, from -2 cm / 0 to entry / 80, with the
  maximum held deeper. Viseme `oh` detection uses public SDK fields and checks
  that the named shape exists. Choosing `None` clears the target shape.
- Reset pose and reset part settings are separate. Settings edits and custom
  additions/removals register Undo on the serialized window state.
- Path controls appear only when the enabled setup contains both mouth and anus.
- Menu visibility, compatibility omission, and initial states are draft settings.
  The preview describes the approved compatibility-ON guard on Instant activation;
  it does not execute it. Runtime parameter ownership and persistence are pending.
- Apply and IcePop placement are disabled. No avatar hierarchy, animation,
  material, source model, or dependency state is changed by this window.

The original PoC generator is preserved with its original window under
`Tools > LumaKroma > SPS2 Setup Assistant (0.1 PoC)`.

## Validation boundary

The package static validator and offline C# compilation against the existing
Unity 2022.3.22f1 validation host's referenced assemblies are separate from an
Editor import. An offline compiler result is not a domain-reload, layout,
Undo/Redo, or VRC result. Actual Unity screenshots, GUI interaction checks,
and Editor Console inspection are still required for this preview.

Full-version generation, native serialization equivalence, three-avatar
placement, VCC installation/removal, Gesture Manager, and VRC PC validation
remain outside this screen-only slice.
