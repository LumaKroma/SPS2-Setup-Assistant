# 1.2.0-beta.4 public delivery checks

This reuses the existing product packaging and beta.3 official delivery procedures. It is an executable handoff for **after public-copy approval and publication**, not an authorization to publish or a new harness.

## Identity and release route

- Functional source: `38599b349b7495348c90df99c5e4d41a52c9e905`; release-preparation source is recorded in the local candidate manifest.
- Expected version: `1.2.0-beta.4`; common package `com.lumakroma.sps2-setup-assistant`. Stable Latest remains 1.1.1.
- Official listing: https://lumakroma.github.io/SPS2-Setup-Assistant/index.json
- Expected four assets: common VPM ZIP, package.json, Fury Installer ZIP, NDMF Installer ZIP. Do not attach private evidence or standalone checksums/unitypackages.
- Recheck tag/release/draft absence and live main before publishing. Use LumaKroma author/authentication. Preserve every previously published version URL/hash.
- Use the approved release body file verbatim. Public page changes start from main `e66759fa7d4aec895dc37814362141d64c4f3104` or its verified successor, retaining current UI. Main-ref listing deployment is the existing supported route.

## Reuse existing delivery fixture

Prior evidence: local sibling `sps2-beta3-publication/official-install/summary.json`, `alcom-result.json`, `final-preservation-exit.json`. Approved reusable project:
`C:/Users/konoh/AppData/Local/Temp/sps2-backend-migration-validation`.

Recheck live process/lock ownership before launch; do not assume yesterday's PID or state. Use the current Unity runbook and existing MCP entry. Snapshot source scene/settings, package files, manifest/lock and dependency inventory before the first install. Leave user projects, RMS and unrelated sessions untouched.

1. Fetch the published package.json and all three ZIPs. Compare their SHA256 with the exact approved candidate. Confirm prerelease=true, Latest=1.1.1, listing beta.4 URL/hash, and all previous listing entries unchanged.
2. Reuse the official Fury VPAI absence/import cycle in the disposable fixture. Extract the public Fury Installer ZIP, import its unitypackage, inspect the native confirmation, then install. Verify installed package version and every payload byte against the public common ZIP. Record actual resolved/embedded dependency versions, final persistent Console, UI opening and minimal native generation.
3. Restore the fixture as required by the existing route, then repeat with the official NDMF VPAI. Do not import both VPAI installers simultaneously: they share the official temporary GUIDs. Record both family selection results.
4. For existing users, begin from an actual installed beta.3 package/settings copy and use the client update route to beta.4. Verify that settings, manual pose/calibration, and Depth Action targets/modes survive loading; run normal Apply, save and reopen. Apply renews the settings snapshot/identity by design: check old asset preservation and new native-reference correspondence, not identical post-Apply GUID.
5. Confirm native Socket names after Apply and standard menu behavior after SDK Build. Keep the existing Disabled restoration and Auto/Force observations as source evidence; do not rerun broad unrelated lifecycle tests solely for metadata changes.
6. In ALCOM, enable prereleases and verify beta.4 visibility. Actually select/apply beta.3→beta.4 and inspect the change plan before confirmation. Previous ALCOM 1.1.8 Select/Invoke returned “Unrecognized error”; visibility alone is not installation. Reuse a supported browser/UI route or let the user perform the native selection if still blocked. Record the specific blocked step; do not substitute copied files for a client-update PASS. VCC standalone remains a separate optional route if not exercised.
7. Finish outside Play, save only owned test evidence, exit normally, verify the owned process is gone, restore package/manifest/lock and verify protected source/dependency hashes. Keep new fixture evidence separately.

The prior beta.3 VPAI checks used embedded existing dependencies in one warm dual-backend fixture. Repeat results must be described with that same scope unless fresh family-exclusive dependency installs are actually performed. No VRC upload is needed for this delivery check; VRC client/remote-player behavior remains a separate gate.

## User-visible changes to verify

- Standard backend menu layout, global labels, defaults and saved flags replace Assistant overrides.
- Menu language applies to Socket names; existing Fury setups require “置き換えずに変更を反映” to adopt their saved override/language.
- Assistant Local Only is hidden; native Stealth remains under the backend's standard menu.
- A rebuild/reupload is needed for already uploaded avatars. Do not promise that old saved toggle state survives the native persistence change.
