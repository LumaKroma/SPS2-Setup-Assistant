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
