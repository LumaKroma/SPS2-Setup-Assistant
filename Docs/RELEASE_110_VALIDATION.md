# 1.1.0 local release preparation — 2026-10-02

## Current migration candidate (supersedes artifact identity below)

After the pre-migration preparation at436e83b, the user approved minimal legacy
migration metadata and disposable validation. The new candidate changes only
package.json and README.md among shipped files. All product C# and .meta bytes
remain unchanged. No new Unity/runtime PASS follows from that byte comparison.

New VPM ZIP:116,391 bytes, SHA256
`00d79b1476580c373a2424a1bbbd9fd1e2364bb390bbc99a7b4833cce2fc63a4`.
Two independent builds match. Real ZIP/listing builder tests preserve exactly
one legacy folder path/GUID and40 shared legacy asset GUIDs. No extra deletion
targets are permitted. Official VPAI DLL/config are unchanged; the customer ZIP
contains updated migration guidance. Evidence:
`.codex-staging/sps2-i18n-110/release-110-migration-candidate/migration-candidate-audit.json`.

Native migration, confirmation/Cancel, moved/modified-folder deletion, backup
restoration and settings/scene reimport tests have NOT RUN. They require the
reviewed isolated project/import/prompt scope in MIGRATION_NATIVE_SCOPE_20261002.md
in private evidence. Do not promote this candidate as migration-validated.
See LEGACY_MIGRATION_110.md for the exact target and data-loss boundary.

The sections below record the completed PRE-MIGRATION candidate and its hashes.
Their documentation-only and byte-identity statements apply to that historical
candidate, not the new manifest/README changes.

## Pre-migration candidate evidence

Version 1.1.0 remains unpublished. Product code tested in Unity is
`b042e1628b219b4414e348015fcae60679956938`; subsequent release preparation changes
documentation only. The release audit compares every shipped package byte to
that tested baseline and records the final preparation revision separately.

## Latest authoring regression

- Formal v5 same-Editor update passed with no restart. Dedicated Editor PID
  51540 and lease `8aa61da18d2249678c9c76e91f378eed` retained their identities
  through the update and all tests; no RMS or Shared settings were changed.
- Native Unity EditMode FullSetupSettingsTests: 46 passed, zero failed/skipped,
  including 28 regeneration reference-guard cases.
- Actual nonreplacement Apply preserved all 14 Socket object identities and
  serialized settings. A generated-object Depth Action reference was retained
  when its part remained included.
- Actual regeneration rejected Object and inactive renderer references into
  the destroyed hierarchy before changing hierarchy, settings, caller input,
  window drafts, selection, Undo group or settings-asset inventory.
- Supported regeneration retained external Body renderer, external hand bone,
  AnimationClip and retained test Plug references through scene save/reload.
- Original scene, settings and window draft were restored exactly. All 1,370
  files hashed before the integration probe remained unchanged; 12 new files
  belong to the isolated working scene, reference clip and settings snapshots.
- Final formal State at 2026-10-02T00:38:15.5590614Z showed idle Edit, no dirty
  scene, no compile/update/play transition, and no retained uncertainty.
  Normal Close completed, PID exit was observed, and formal audited Release
  succeeded. No forced termination or manual lock operation occurred.

Evidence in the private management workspace:
`.codex-staging/sps2-i18n-110/depth-guard-final-audit.json`,
`depth-guard-evidence-20261002/`, `depth-guard-final-state.json`,
`depth-guard-close.json`, and `depth-guard-release.json`.

## Prior runtime double-check and user report

On `92886f73db17ae6382093cc2c717f82a8a4881fd`, whose runtime/build integration
code is unchanged by the guard, fresh dedicated checks passed 56 Socket
ON/OFF steps and 238 Legacy/Stealth/root-OFF steps. Native global curves,
foreign curves and existing Write Defaults settings were retained. Actual
foreign-Socket mixed-avatar behavior remains unverified. The native
integration observed was VRCFury 1.1403.0 with Unity 2022.3.22f1. This does not
certify every VRCFury release. Evidence: `doublecheck-final-audit.json` and
`doublecheck-evidence-20261001/` in the same private evidence directory.

The user explicitly reported real VRChat testing on 2026-10-01 at 23:10 UTC.
Their exact artifact, avatar and complete scenario matrix were not supplied;
record this report separately from automated tests. It is not an exact-final-
archive fresh-install or universal avatar/language/Auto PASS.

## Local artifacts and remaining boundaries

The VPM ZIP includes 80 entries. Two independent builds produce SHA-256
`fc15a3140ac99de99fbf9d8ecda40e06ed2b2ab13df177341dcf97bfde19f46e`.
Product bytes and stable .meta files are preserved. Tests, internal contracts,
private evidence and avatars are excluded. The VPAI installer uses the observed
official creator 1.1.6 with an exact 1.1.0 dependency; its audit and distribution
hashes are retained beside the artifacts in `release-110-local/`.

Remaining checks: final published-version VPAI/VCC/ALCOM fresh installation,
native-language quality review, other-avatar visual fit, and any scenario not
covered by the user's real-client report. Tooltip visibility was not established
by the available observation route; string coverage is not a visual PASS.

Ring calibration still rejects non-unit world joint frames, including ordinary
whole-avatar uniform scale other than one, nonuniform bone scale, shear and
nonfinite inputs. No scale support expansion is claimed. Saved calibration is
not silently replaced. Ordinary Apply that removes a part referenced by another
Depth Action remains a separate unverified boundary; the new guard applies to
explicit regeneration.

No Auto-policy change, legacy-folder removal, upload, publication, push or merge
was performed. Historical releases remain immutable. Publication needs a
separate explicit decision naming the destination and exact artifact hashes.
