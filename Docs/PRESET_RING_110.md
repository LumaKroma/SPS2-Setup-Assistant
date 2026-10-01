# FingerRing preset contract amendment — unpublished 1.1.0

Issue180 Context Check: https://github.com/LumaKroma/LumaGimmics/issues/180#issuecomment-5935508726 . User approved 2026-10-01.

This amendment supersedes only the statement that existing presets leave finger rings untouched in `Packages/com.lumakroma.sps2-setup-assistant/Documentation~/OUTPUT_CONTRACT.md` (Experimental finger-ring calibration).

Applying Full includes both `fingerRingLeft` and `fingerRingRight`. Applying Casual or Default excludes both. New/default and upgraded legacy configurations still start with missing rings OFF. Manual individual selections remain available and survive copy, serialization and reload until another preset is explicitly applied.

Preset changes modify only fixed-part inclusion. All saved ring calibration frames, weights, offsets, rotations, menu-name overrides and depth settings remain intact. Custom parts and existing ordinary preset memberships keep their previous behavior. Auto flags, ring Auto exclusion and the existing16 limit remain unchanged. No version bump, generation, migration deletion or release change.

The first same-Editor pilot admits two existing C# paths only. Package documentation bytes remain outside that reviewed synchronization scope; retain this root development contract as the active amendment and fold its wording into packaged documentation at the next separately reviewed package-content synchronization before release. This documentation gate does not authorize a broader live overwrite.

Focused regression covers every preset with both Auto flag values, repeated Full/Casual/Default sequences, both ring sides, ordinary/custom memberships, calibration/name preservation and manual selections through copy/JSON/upgrade. Native-language, other-avatar and VRC PC acceptance remain separate.
