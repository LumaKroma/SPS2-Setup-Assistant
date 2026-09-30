# Changelog

## 1.1.0 — Unreleased
- New paired standard Socket adjustment transforms use unique `SPS_*Socket_L/R` names. Existing names and paths remain unchanged on Apply and regeneration; no automatic migration of user animation bindings.
- UI and generated menu language selection: Japanese, English, Korean, Simplified Chinese and Traditional Chinese.
- Per-socket menu-name override, preview and reset in the setup window. Overrides survive presets, language changes, saved settings, Apply and regeneration.
- Existing settings retain Japanese menus; previously authored names migrate without replacing standard identities.
- Source-only implementation; Unity and VRC PC validation remain pending.

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
