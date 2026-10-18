# VPM release handoff

## 1.2.0-beta.4 preparation (2026-10-18, unpublished)

Prepare the next beta from functional commit
`38599b349b7495348c90df99c5e4d41a52c9e905`, preserving its native NDMF delegation
and Disabled-only Fury restoration. User approval of the public copy is pending.
Do not push, create a tag/draft Release, publish, or update VPM/Pages before that
approval. Git author and authenticated wrapper must resolve to LumaKroma.

Reuse Build-VpmPackage, Build-FamilyInstallers and Test-FamilyInstallers with the
official VPAI 1.1.6 creator/template. Create fresh beta.4 dev.1 directories, build
twice from clean exact source and compare all five archive hashes. Preserve beta.3
artifacts, dependency ranges, legacy folder GUID and installer licenses. Product
C#/asmdef source must remain byte-identical to the validated functional commit.

Live inventory at preparation: beta.3 is published; beta.4 tag/release/draft is
absent; stable GitHub Latest is 1.1.1. Intended public attachments are the common
VPM ZIP, package.json, Fury Installer ZIP and NDMF Installer ZIP only. Keep
prerelease=true and make_latest=false. The existing release workflow creates only
the first two attachments; audit/upload the two prepared installer ZIPs separately.

The public page baseline is main
`e66759fa7d4aec895dc37814362141d64c4f3104`; preserve its styling and installation
flow. Never deploy this product branch's older Website. The local page proposal
changes beta links and one release summary only. After approval, prepare the page
from live main and use the existing main-ref Pages workflow; tag-ref deployment
was rejected previously. Publishing triggers listing.yml, so coordinate page and
listing publication and retain all previous version URLs/hashes.

Post-publication checks reuse the existing beta.3 official VPAI and ALCOM route;
see `Docs/RELEASE_120_BETA4_DELIVERY.md`. Prior warm-project VPAI passes do not prove
fresh dependency installation. ALCOM version visibility passed previously, but
its Select/Invoke update failed before confirmation; do not label that path PASS.
No Unity startup or new shared validation infrastructure is needed for preparation.

## 1.2.0-beta.3 preparation (2026-10-17, unpublished)

Prepare one common VPM package and the existing official Fury/NDMF VPAI installers
from the independent product source. Use new local dev.1 output directories and
preserve published beta.2/stable artifacts, GUIDs, dependency ranges and legacy
migration boundaries. The reviewed functional patch is
6d1922254718e82e569fec62c98b214cddafb420d912da8ef63d6c1d79cb7837 on
c3d25d24b1bdc3f306df68e74e8d6d49365f6d8e; subsequent changes prepare metadata/docs
only. Record the resulting local source commit and actual archive hashes in the
candidate evidence. Do not dispatch release/listing workflows or create a tag.

Track SPS2-NDMF-JSON-REF-20261017 in Docs/KNOWN_ISSUES.md. The full test result
remains 206 passed / 2 failed, with identical failures reproduced on baseline.
Before publication, determine actual Object-reference save/reload/update impact
or document a separately reviewed disposition and explicit scope. Do not change
the tests to expect null or omit their failures from release validation.

Existing native Play, real-rig reference/open/return and saved calibration evidence
does not certify VRC PC, authored Gestures, final materials/prefabs or fresh
installation of the unpublished package. Installing through the generated VPAI
requires publication in the listing and remains a separate delivery gate.
Candidate creation/inspection authorizes no publication or promotion.

## 1.2.0-beta.1 preparation (2026-10-11, unpublished)

Current public stable is 1.1.1 (tag commit
7a08706298b91b678a8324b4e60186792cebf539). The public release/tag/listing inventory
contains no earlier beta convention. The proposed minor prerelease adds a new
optional SPS family without promoting or replacing stable 1.1.1.

Candidate source is Issue186 b6ff22b6c73a4d78fde272ef12439c16135ba9f7 plus the
uncommitted family UI/localization/policy-test and spacing edits, followed by this
metadata/documentation preparation. Commit and review the exact combined diff
before any public action. The live user validation project remains untouched by
this preparation. User manual PASS is scope-unspecified, not VRC certification.

Build the existing common VPM package ZIP and package.json for this beta. Do not
bundle Fury, SPSNDMF, MA, NDMF, lilToon, Gesture Manager, test avatars or evidence.
The user's subsequent distribution decision adds two official VPAI installers,
Fury and NDMF, sharing this exact beta package. See Docs/RELEASE_120_BETA1_DISTRIBUTION.md.
Their configurations use includePrerelease=true and silentIfInstalled=false.
The current stable VPAI configuration remains unchanged. No new Assets edition or
VPM metapackage is introduced.

After publication approval and review of the intended license scope:

1. Review/commit the entire candidate on the Issue branch. Static/package checks
   and two deterministic ZIP builds precede draft creation. The package README
   must accurately retain remaining gates. Do not run Unity in the user's Editor.
2. Recheck tag/release absence, including authenticated drafts. Dispatch the
   existing release.yml at the reviewed candidate commit/ref, creating tag
   1.2.0-beta.1 and a draft with prerelease=true. Verify target commit, assets and
   hashes. Publish with make_latest=false; stable 1.1.1 remains GitHub Latest.
3. The existing Prepare-VpmListing includes all public non-draft semver releases,
   including prereleases. Use the existing SPS2 listing at
   https://lumakroma.github.io/SPS2-Setup-Assistant/index.json, after reviewing
   the deployment ref. Its prior-version SHA guards must preserve every existing
   entry. Listing publication can trigger automatically; review that route before
   publishing. Under the recorded Pages policy, dispatch listing.yml on reviewed
   main; do not rely on a tag-ref Pages deployment. Live main metadata currently
   remains older than stable and is not a release-source baseline.
4. Verify public release prerelease=true, GitHub Latest still 1.1.1, stable entry
   ZIP hash d31a5d61c52c6fa17e64e39d736d8dc5a1372e86eba17ee0b9cb5e09a735e2af,
   beta manifest/hash and explicit prerelease visibility in VCC/ALCOM. Fresh
   package installation is a delivery gate, not proven by local ZIP inspection.

Minimum remaining regression: audit the final ZIP contains the exact UI and
spacing code and excludes dependencies/tests; check the installed-family matrix,
saved/missing family compatibility and existing-family guard on the final committed
source if executable code changes again. Do not repeat SDK/Controller builds for
metadata/README-only edits. Native final-ZIP NDMF import plus one legacy-Fury
load/Apply check remains distinct from the user's current manual PASS. Any broader
Undo/save-reopen/lifecycle/VRC scope requires a separate scheduled validation slot.

SPSNDMF 1.1430.0 declares VRCFury License (Personal / VRCA-VRCW). Its commercial
terms restrict relying on modified/derivative Fury and directing users to it.
No license clearance is inferred from omitting dependency binaries or calling the
release beta. Confirm the intended distribution/use scope and any applicable
rightsholder permission when making commercial-use claims. No individual author
permission requirement was identified for noncommercial free API integration
without dependency redistribution/modification; commercial users are a separate scope.

## Current 1.1.0 preparation (2026-10-02, unpublished)

The current distribution is the official VPMPackageAutoInstaller (VPAI)
unitypackage plus VCC/ALCOM. Do not build a new legacy Assets-edition archive
with Prepare-UnityPackage.ps1. Historical archives and releases stay immutable.

Build the VPM ZIP through Tools/Build-VpmPackage.ps1 in two fresh directories and
compare SHA-256. The official observed VPAI creator is version 1.1.6, source
965a08e896ce39790fd0d46bcb89d2ffba99ea13. Prepare a fresh configuration with
com.lumakroma.sps2-setup-assistant = 1.1.0, lilToon >=1.0.0, Unity 2022.3,
includePrerelease=false and silentIfInstalled=false. Repository URLs are the
SPS2 listing below, https://vcc.vrcfury.com/ and
https://lilxyzw.github.io/vpm-repos/vpm.json. Use its official creator.mjs;
audit the three installer entries (folder, DLL and configuration), their GUIDs,
configuration and DLL bytes. Include the VPAI and semver.net licenses in the
customer ZIP; do not include private evidence, avatars or source repositories.

This installer resolves published packages. A local archive audit does not prove
fresh installation of unpublished 1.1.0. Actual VPAI/VCC/ALCOM delivery remains a
separate check after explicitly approved publication. No upload, push, merge or
listing change is authorized by this preparation.

The manifest now declares exactly Assets/LumaKroma/SPS2SetupAssistant and its
observed root GUID in legacyFolders. No legacyFiles/legacyPackages are declared.
This uses standard VPAI/VCC/ALCOM migration. Keep VPAI's normal confirmation and
never enable NO_PROMPT. Path-first recursive removal includes user additions;
require an external whole-project backup and old-folder inspection in guidance.
Settings at Assets/SPS2Settings remain outside the deletion target. Already-
installed early exit does not guarantee later duplicate cleanup. See
Docs/LEGACY_MIGRATION_110.md for the approved boundary and required disposable
native cases. Artifact tests alone do not pass those cases. Earlier artifacts
without migration metadata are superseded only after the new candidate passes
its own review; never overwrite historical published releases.

See Docs/RELEASE_110_VALIDATION.md for exact validation and remaining limits.
The following 1.0.x sections are historical procedures, not current 1.1.0
authorization or a requirement to produce an Assets-edition package.


## Native unitypackage distribution

The 1.0.1 unitypackage addition is owner-approved (2026-09-20). Keep the published
VPM ZIP immutable. `Tools/Prepare-UnityPackage.ps1` verifies that ZIP's SHA256 and
stages it in a fresh Unity project under `Assets/LumaKroma/SPS2SetupAssistant`.
Only the exact `FullSetupGenerator.PackagePath` declaration changes; asset and
script GUIDs are preserved. A Japanese `UNITYPACKAGE.md` explains prerequisites,
fixed installation path and switching between mutually exclusive VPM/Assets installs.

Capture the script's JSON output as the staging manifest. Use Unity's public
`AssetDatabase.ExportPackage` on that exact root with `Recurse`, never
`IncludeDependencies`. Audit with `python Tools/Test-UnityPackage.py <archive>
<staging-manifest.json>`. Verify clean import, compilation, test Plug assets and
generation in an isolated dependency-equipped project before uploading the native
archive to the matching GitHub Release. Do not distribute validation avatars,
Editor tests, dependencies or internal evidence. Record validation separately;
Editor import checks do not establish VRC PC PASS.

Standalone SHA256SUMS files are internal build evidence, not Release attachments.
Retain ZIP hash validation and the VPM manifest's zipSHA256. Under the existing
Pages environment policy, dispatch listing.yml on reviewed main after publishing;
release-tag deployment is not permitted by that environment.

## 1.0.1 patch (approved 2026-09-20)

The owner authorized publishing the breast placement repair in Issue 180 as a
patch release. Recognize numbered breast bone chains and exclude independent
outfit skeletons from body breast selection. Existing generated positions remain
editable and require explicit regeneration to adopt the new automatic placement.
The tested implementation is 9c5f4e9ba2bb1b9be674d02d59961f60fe2b84ac;
release preparation changes metadata only. See Documentation~/VALIDATION.md for
33/33 Editor tests and real avatar comparison evidence. VRC PC, Airi and a fresh
1.0.1 VCC/ALCOM installation remain unverified; publication approval is not a
runtime validation result. Preserve the published 1.0.0 release and listing entry.

## Historical 1.0.0 approval

Version 1.0.0 is approved for publication by the owner on 2026-09-13, from
implementation baseline 5ddff4837122d53526112f01d6f1c6732961cd65 (Issue121).
The owner reported VRC PC PASS and accepted the final feature removals.
Public VCC/ALCOM installation remains a distinct delivery check.

## Build and validate locally

Use PowerShell7: `./Tools/Validate-Package.ps1`, then
`./Tools/Build-VpmPackage.ps1 -OutputDirectory .artifacts/vpm`.
The ZIP puts package.json at its root, preserves Assets/Editor/Runtime bytes and
metadata, and includes MIT license and the Japanese README. Tests, private
contracts/evidence and repository tooling are excluded. The script checks source
byte equivalence, metadata and LFS pointers. Use a new directory for each build;
repeat outputs must have identical SHA256 on the same PowerShell/.NET runtime.

## Remaining human gate

Test this exact package in VRChat PC, particularly Socket ON/OFF, menu actions,
guided-path Collapse and test Plugs. Existing avatar-base
mixed Write Defaults may still produce VRCFury's own warning. Record avatar,
Unity/dependency versions, source SHA and results in Issue121. No passing release
claim follows merely from Editor tests or SDK preprocessing.

## First publication

After the runtime gate and the user's publication decision, select the release
version (current dev prerelease remains available for testing). Update the
manifest and version validation together, remove the README preparation notice in
both copies, commit and review the exact result. Merge and public visibility need
their own approved operation; do not expose the private management repository.

For LumaKroma/SPS2-Setup-Assistant, enable GitHub Pages with source GitHub Actions.
Run **Prepare VPM release draft** on the reviewed commit. It refuses existing tags
and release versions, uploads ZIP/package.json/SHA256SUMS and leaves a draft.
Verify the target commit and all assets before publishing that draft. The
**Publish VPM listing** workflow runs on publication (or manually); it only runs
for a public repository and only lists public, non-draft release assets. It
rebuilds all versions in the official VPM JSON format and verifies the full
expected version set before Pages deployment. Never delete old releases or replace
same-version ZIPs. Fix a partial draft explicitly; no silent overwrite/retry.

The URLs are:
- https://lumakroma.github.io/SPS2-Setup-Assistant/
- https://lumakroma.github.io/SPS2-Setup-Assistant/index.json

Finally, use a fresh VCC/ALCOM avatar project, register VRCFury and lilToon sources,
install dependencies and this package through the listing, then verify generation
and updating/reimport without missing scripts or pink test materials. A ZIP unpack
check is not proof of live VCC/ALCOM delivery. Record HTTP/manifest/ZIP hash and
installation results; keep Issue121 open until the required gates pass.

## Implementation notes

The official package-list-action at cb31c3b5d17d1070d7741c61de2ca1b219224039 was
inspected and restored in a disposable folder. Its pinned dependencies emitted
NU1904/NU1903 advisories (including Scriban5.5.0). It also enumerates draft releases
with authenticated access and skips existing URLs on rebuild. It is not a CI or
product dependency in this implementation. A small PowerShell/.NET ZIP+JSON builder
uses the documented VPM schema instead, with no custom authentication or renderer.
Public non-draft release ZIPs are validated against exact package/tag/filename,
root manifest and SHA256. Every expected version must be present; previously
published versions cannot disappear or change SHA. Deployment stops on failure.
GitHub Actions/Pages and fresh VCC/ALCOM delivery remain unexecuted until the gate.
