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
