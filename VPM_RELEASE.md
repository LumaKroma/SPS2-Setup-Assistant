# VPM release handoff

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
