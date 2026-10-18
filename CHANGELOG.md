## 1.2.0-beta.4 - Prepared, unpublished (2026-10-18)

- Delegate NDMF builds to the native SPSNDMF/MA/NDMF pipeline.
- Restrict Fury FX restoration to native unresolved Write Defaults under effective Disabled, including one-time Skip; Auto/Force builds skip Assistant authoring lookup.
- Use native menus, defaults and saved flags. Save localized/overridden Socket names during generation and Apply; existing Fury setups need Apply to adopt those names.
- Remove the Assistant Local Only switch in favor of native Stealth. Keep prior settings data and copy-on-write identity behavior.
- Source behavior: 38599b349b7495348c90df99c5e4d41a52c9e905. Preparation changes metadata and documentation only. VRC client and final published installation remain separate gates.

## 1.2.0-beta.3 - Prepared, unpublished (2026-10-17)

- Warn after successful calibration when the estimated fingertip gap exceeds the existing 0.2 guideline; retain strict geometry/frame checks and saved/manual calibration.
- Report distinct, localized automatic-placement rejection reasons without assuming an incorrect Humanoid mapping.
- Keep the existing reference muscles, 0.6 tip extension, 1.6 length factor, tracking frames, weights and serialization schema. No temporary Avatar range standardization or distal-local tip trial is included.
- Native Play cases pass 5/5. Package regression is 206 passed and 2 failed; both NDMF Object-reference roundtrip failures also reproduce on the unchanged baseline. Track them as SPS2-NDMF-JSON-REF-20261017; actual save/update impact is unresolved.
- Real FBX native-constraint pose and save/reload observations are distinct from SDK avatar builds and VRC client Gesture certification. No publication is authorized by this preparation.

## 1.2.0-beta.1 - Prepared, unpublished (2026-10-11)

- Add explicit optional VRCFury / SPS for NDMF family selection for new setups,
  preserving old Fury settings and rejecting existing-family conversion.
- Guard package identity, shipped NDMF schema and managed DepthActions; separate
  NDMF processing from Fury SDK callbacks. Remove mandatory VRCFury VPM dependency.
- Default new drafts to the only usable installed family. Disable unavailable
  choices with their capability reason; allow both when both are usable.
- Keep saved/generated family unchanged, including legacy settings without a
  family field. Add spacing between the family dropdown and information box.
- The user reported manual PASS on 2026-10-11 for the latest NDMF/UI candidate;
  test scope and VRC platform were unspecified. Earlier SDK/Controller evidence
  predates the UI delta. Native UI compile and 15 focused assertions were checked,
  with normal/narrow spacing checked after the subsequent layout-only change.
- Stable 1.1.1 stays unchanged. This prerelease candidate is not published.
  Exact packaged delivery, broader lifecycle/VRC checks and dependency license
  confirmation remain separate gates; no VRC runtime certification is claimed.

# Changelog

## 1.1.1 - Unreleased

- Support authored positive uniform scale for finger rings, preserving legacy unit-scale calibration and refreshing native distances on Apply.
- Generate with yellow, five-language manual-adjustment guidance for nonuniform/negative scale. Finger-ring position, orientation and size are not guaranteed; initial placement uses the current finger frames, while saved calibration is reused.
- Skip only a finger ring whose coordinate transform cannot be safely inverted. Preserve its saved calibration and continue other valid parts. If nothing remains, or the avatar root itself cannot be safely inverted, keep existing output/settings unchanged and report a yellow notice.
- Reapply after hierarchy scale changes. Runtime scale animation is outside this patch; palm/Auto policy is unchanged.
- The warning-only revision passed 128 related Unity tests, 62 warning/skip/no-change generation checks and 177 positive-uniform generation checks across 7 cases. Offline compile/package checks and 206 five-language entries also passed. See Documentation~/VALIDATION.md.

## 1.1.0 - Unreleased

- Setup UI and generated menus support Japanese, English, Korean, Simplified Chinese and Traditional Chinese. Menu language is independent of UI language; per-Socket overrides survive presets, saved settings and regeneration.
- Newly generated paired adjustment transforms use unique `SPS_*Socket_L/R` names. Existing names and animation paths are preserved without automatic renaming.
- Add optional left/right finger loops, separate from palm Sockets, with initial per-avatar estimation and calibrated weighted finger following. Full includes both loops; Casual and Default exclude them. Saved calibration and names survive preset changes. Loops remain excluded from Auto; its existing policy and 16-Socket limit are unchanged. Scaled rigs remain unsupported and invalid calibration fails closed.
- Repair Socket OFF and Legacy/Stealth restoration for the observed native integration, preserving native/foreign curves and Write Defaults settings.
- Reject regeneration before changing data when a Depth Action references a generated object or renderer that regeneration would destroy. References outside that hierarchy and retained test Plugs remain supported. Ordinary Apply that removes a referenced part is a separate, unverified boundary.
- New distribution uses the VPM installer unitypackage and VCC/ALCOM. No new Assets-edition package is distributed. Exact legacyFolders metadata supports removal of the old Assets product folder during migration; back up the project and inspect user modifications before accepting.
- Unity authoring regression: 46/46 EditMode tests plus actual Apply/rejection/save-reload checks passed on b042e16. Prior runtime double-check on unchanged runtime code passed 56 Socket-toggle and 238 Legacy/Stealth steps. The user separately reported VRChat testing; exact final-package delivery and native-language review remain open. See `Docs/RELEASE_110_VALIDATION.md`.

## 1.0.2 — 2026-09-22
- 貫通がオフのとき、口と肛門のソケット種別を Auto に変更。貫通がオンの場合は Ring を使用します。
- 既存セットアップも設定を適用すると更新されます。手動調整した位置は維持されます。

## [0.1.0] - Unreleased

- Add the private Socket-only, Humanoid-only proof-of-concept package skeleton.
- Add deterministic eight-location planning and two public-API attachment
  profiles.
- Add a one-button Editor window, grouped Undo transaction and tool-owned root
  replacement boundary.
- Include every generated authoring component in the grouped Undo transaction.
- Add EditMode test sources and deterministic package/source validation.
- Keep Modular Avatar optional through a version-defined Editor integration;
  the VRCFury-only profile remains available without MA.
