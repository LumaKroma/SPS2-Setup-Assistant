# 1.2.0 validation scope - 2026-11-11

This interim SPS2 release retains the product code of corrected official beta.5
47c5c0ff51897a914e125ae491fecbc8e2f198ed. The NDMF Installer pins SPS for NDMF
1.1436.0. All other dependency/configuration fields, including
includePrerelease=true, are unchanged. No formal SPS3 or VRChat runtime
compatibility claim follows from these checks.

## Installer delivery and compilation

Observed on Unity 2022.3.22f1 with official VPAI 1.1.6:

| Installer | Starting Assistant | Result | Resolved backend | VRChat SDK | Other resolved packages |
| --- | --- | --- | --- | --- | --- |
| Fury | Fresh | Installed 1.2.0; no C#/Installer errors | VRCFury 1.1453.0 | 3.10.6 | lilToon 2.4.1 |
| Fury | 1.1.1 | Updated to 1.2.0; no C#/Installer errors | VRCFury 1.1454.0 | 3.10.6 | lilToon 2.4.1 |
| Fury | 1.2.0-beta.5 | Updated to 1.2.0; no C#/Installer errors | VRCFury 1.1454.0 | 3.10.6 | lilToon 2.4.1 |
| NDMF | Fresh | Installed 1.2.0; no C#/Installer errors | SPSNDMF 1.1436.0 | 3.10.6 | MA 1.19.0-rc.0; NDMF 1.15.0-beta.5; lilToon 2.4.1 |
| NDMF | 1.1.1 | Updated to 1.2.0; no C#/Installer errors | SPSNDMF 1.1436.0 | 3.10.6 | MA 1.19.0-rc.0; NDMF 1.15.0-beta.5; lilToon 2.4.1 |
| NDMF | 1.2.0-beta.5 | Updated to 1.2.0; no C#/Installer errors | SPSNDMF 1.1436.0 | 3.10.4 retained | MA 1.19.0-rc.0; NDMF 1.15.0-beta.5; lilToon 2.4.1 |

The NDMF update from 1.1.1 retained the installed VRCFury 1.1452.0. Package
installation is not automatic conversion of existing Fury settings to NDMF.
The beta.5 NDMF update retained SDK 3.10.4 because it satisfies the dependency
range; it was not an SDK 3.10.6 test. MA and NDMF prereleases were actually used.

The Fury runs used an earlier documentation revision of the common package;
the NDMF runs used the subsequent documentation revision. Both contained 88
entries, and the installed files exactly matched the respective tested ZIP.
Those revisions and this release's common ZIP differ only in README.md.
Product code, package manifest, assets and metadata are identical. Official
Installer code and dependency requests match the isolated delivery checks;
those checks did not exercise the production Assistant repository URL. The
production raw installers are unchanged from the preceding distribution, and
current customer ZIPs update README.txt only. These comparisons carry forward
the stated code/dependency results, not a claim that the final documentation
ZIP was reinstalled or that a public endpoint was tested.

## NDMF update and saved-setting checks

A representative beta.5 / SPSNDMF 1.1430.0 setup was saved before the update.
After the Installer update to 1.2.0 / SPSNDMF 1.1436.0, loading that setup,
applying settings, and saving/reloading preserved the checked SetupSettings,
Object ON/OFF targets, BlendShape and Clip references, hand-adjusted Socket
poses and oral-boundary setting. Pose tolerances were 1e-6 for position/scale
and 0.01 degrees for rotation. This is observed semantic retention, separate
from the unchanged source-asset hash checks.

Ordinary, duplicate-Socket and duplicate-Plug SDK preprocessing passed after
the update. Duplicate Socket menus changed Name / Name1 / Name2 to
Name / Name_2 / Name_3, with corresponding generated OSC/haptics parameter
suffix changes. The updated duplicate-Plug build likewise used Name_2/Name_3.
Saved Socket toggle state and external OSC/haptics mappings may need adjustment;
their VRChat behavior was not tested.

The older duplicate-Plug baseline failed on material loading. lilToon was added
and MA/NDMF also changed during the update, so the later passing result cannot
be attributed solely to the SPSNDMF pin. These are representative preprocessing
checks, not full-avatar SDK bundle or runtime tests for every saved setting.

## Local real-avatar SDK builds

- Fury: MANUKA with VRCFury 1.1454.0, SDK 3.10.6, lilToon 2.4.1,
  MA 1.18.1 and NDMF 1.14.3 completed an SDK Build. The copied avatar used
  Disabled Write Defaults handling, equivalent to the temporary Skip choice.
- NDMF: new default SPSNDMF 1.1436.0 generation on a previously unconfigured
  MANUKA prefab, followed by save/reload and SDK Build, completed with SDK
  3.10.6, MA 1.19.0-rc.0, NDMF 1.15.0-beta.5 and lilToon 2.4.1.
  The target had 12 NDMF components, no Fury components and no missing scripts.
  VRCFury 1.1454.0 was also installed in the project. This is not an
  NDMF-only-package-stack result or a conversion of an existing Fury setup.
- Existing Fury-to-NDMF conversion stopped before SDK Build on a depthActions
  enableOthers mismatch (actual true, expected false). The observation does
  not establish user editing as the cause. No conversion success is claimed.

## Packaging and limits

Package/static validation, localization, official Installer payload checks,
paired deterministic builds, GUID/product/history preservation, whitespace and
frozen-listing schema validation pass. The frozen-listing check is not a live
public delivery check. The final documentation-only archive was not reinstalled.

Legacy OFF remains a known Fury limitation. No new full EditMode matrix or
VRChat runtime test was performed for this release. The October 206-pass /
2-failure record and November five-environment 228-pass / 0-failure /
14-skip matrix apply only to their recorded revisions. See Docs/KNOWN_ISSUES.md
for the historical two-test disposition and current compatibility limits.

<!-- Historical record below; not current release readiness. -->

# 1.2.0-beta.3 dev.1 candidate audit — 2026-10-17

Product source is 756ce732fcb256f01e7c0c21b0a13cd62d6d1e4b. Two clean-source
builds match for the VPM ZIP and both official family installers/customer ZIPs.
See [the final candidate audit](Docs/RELEASE_120_BETA3_VALIDATION.md) for hashes,
93-entry/52-GUID checks, source-byte correspondence, retention scope and residue.
The executable result remains 206 PASS / 2 FAIL, with both failures reproduced
on baseline; track [SPS2-NDMF-JSON-REF-20261017](Docs/KNOWN_ISSUES.md).
Fresh final-version installation/update and VRC client gates remain unverified.
No publication, listing update, upload or Unity startup was performed for packaging.

The following records are historical evidence for their stated revisions.

# VPM preparation evidence — 2026-09-13

Source baseline40642de3108d603d668bf03eb405856321f88668; Issue121. No runtime change.
Two product ZIP builds each contain85entries with matching source bytes and SHA:
ec0c5fd62d5dcac7310edb75f4b03227d0bd90c3e268c219adf614b0aaac1692.
Both have root manifest, preserved asset/folder metadata, no LFS pointers, tests,
private contracts or avatar/dependency assets. Package validator passes.

Two-version localhost HTTP fixture (dev.1 and synthetic dev.2) retains both
versions and the source ZIP hash. Wrong manifest version and missing root manifest
reject. Official vpm-core-lib shipped at package-list-action revision
cb31c3b5d17d1070d7741c61de2ca1b219224039 successfully reads and saves the generated
index with both versions and hashes. Official GetAll default omits prerelease
versions; roundtrip checks its serialized version map instead. No stable release
claim follows. PowerShell AST and all three workflow YAML parse checks pass.
Independent read-only reviewer vpm_review checked workflow/packaging/source schema;
its author-string correction is incorporated and official roundtrip passes.

Official automation was evaluated first; NuGet restore reported NU1904/NU1903
(including Scriban5.5.0), and source review found token-visible drafts and skipped
old URLs. It remains only in disposable scratch and is NOT a runtime/CI dependency.
Minimal native PowerShell ZIP/JSON generation implements the observed official
schema. YAML parser was also disposable, not product or workflow dependency.

Unverified gates: GitHub workflow execution, public HTTP endpoint, fresh packaged
Unity import, VCC/ALCOM install and final-head VRC PC behavior. Warm Unity tests
38/38 passed at the prior audit; distribution source bytes equal compiled code,
but byte identity is not a fresh-install guarantee. Keep original avatar copies
and user's scene intact. No public release/Pages/repository visibility action.
