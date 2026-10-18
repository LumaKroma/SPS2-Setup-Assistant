# 1.2.0-beta.4 dev.1 candidate audit — 2026-10-18

Status: local candidate prepared; public wording awaits user approval. No push,
tag, Release, VPM/Pages update or Unity startup occurred in this preparation.

- Source: `e1fd1f2e50011a337e85959ed6733450c48e98eb`; functional source
  `38599b349b7495348c90df99c5e4d41a52c9e905`. All 114 C#/asmdef files match the
  retained validated code inventory. Author: LumaKroma. This record is added later
  and does not change archive inputs.
- Clean detached export checkout; existing package validator/whitespace checks
  pass. Two builds reproduce all five archives exactly. Common VPM ZIP SHA256:
  `374ca96c40cce27866a37d5cfc53aea3e5f73ee7fc727ba03c85bd6fcbdb7ec8`.
- ZIP has 88 source-matching entries. Compared with published beta.3, only the
  five intentionally removed NDMF plugin/assembly/folder entries disappear;
  all 49 surviving meta files match. No new payload paths, dependencies, tests,
  private contracts or avatar evidence. Manifest changes only version;
  Runtime settings data and the storage serialization section remain unchanged.
- Official VPAI 1.1.6 creator/template hashes verified against the existing pins.
  Each installer has the same three assets/GUIDs; only config differs from the
  official template. Dependency ranges, confirmation behavior and licenses stay
  unchanged. Customer ZIPs contain exactly installer + README + two licenses.
- Candidate and full hashes: workspace sibling
  `deliverables/SPS2-Setup-Assistant-1.2.0-beta.4-dev.1/CANDIDATE.json`.
  Public-copy proposal/evidence: sibling `sps2-beta4-preparation/`.
- Existing exact-code Fury/NDMF validation is in package VALIDATION.md. Final
  beta.4 published installation/update and VRC client checks remain unexecuted.
  Reuse [the delivery procedure](Docs/RELEASE_120_BETA4_DELIVERY.md), retaining
  the prior ALCOM Select/Invoke limitation and warm-dependency scope.

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
