Updated distribution scope: see RELEASE_120_BETA1_DISTRIBUTION.md. The earlier two-asset-only/no-beta-installer proposal below is superseded by the user's Fury/NDMF installer decision. Commercial-use questions are separate from this noncommercial free package publication; individual author permission was not identified as mandatory for that scope.

# SPS2 Setup Assistant 1.2.0-beta.1 release notes candidate

Unpublished beta preparation. Stable 1.1.1 remains available and remains GitHub
Latest. Publish this candidate as a prerelease only after the pending decisions
in VPM_RELEASE.md. No tag/release/listing has been created by this preparation.

## Proposed public release body

新規セットアップで VRCFury / SPS for NDMF を選べる beta 更新です。
利用できる系列が一つなら既定選択し、利用できない選択肢は理由付きで無効表示します。
系列欄と案内欄の間の余白も調整しました。

既存の保存済み・生成済み設定は元の系列を保持します。系列項目がない従来の設定は
Fury として読み込みます。適用・再生成による系列の移行は行いません。
安定版 1.1.1 は引き続き利用できます。

NDMF 系列の対象は SPS for NDMF 1.1430.0、Modular Avatar 1.18.1 以上・2 未満、
NDMF 1.11 以上・2 未満です。選んだ系列を公式配布から別途導入してください。
Fury 本体・移行 Stub は NDMF のみの環境には不要です。本ツールの VPM 依存から
Fury 本体の自動導入を外し、既存 Fury 連携を維持しています。
lilToon はテストプラグの表示用、Gesture Manager は任意の手動確認用です。
これらの依存やツールは本 ZIP に同梱しません。

2026-10-11 に最新の NDMF/UI 候補についてユーザーから手動検証 PASS が報告されました。
試験項目・VRC 実機の範囲は未指定です。最終配布 ZIP の fresh install、Undo/save-reopen、
広いライフサイクルと VRC 動作は別の確認事項です。既存の自動 SDK/Controller 証拠は
UI 修正前の候補に対するもので、beta ZIP の VRC 認証結果ではありません。

VCC / ALCOM ではプレリリースを表示し、この版を明示的に選んでください。
公開済みの旧 Assets 版からの移行では、既存 README のバックアップ・削除範囲の
案内を確認してください。

Source commit: insert the reviewed final commit before publishing.

## Internal handoff

- Base: b6ff22b6c73a4d78fde272ef12439c16135ba9f7, including Issue186 implementation
  c1c093f, descended from public stable tag commit 7a08706298b91b678a8324b4e60186792cebf539.
- Additional executable changes: only family UI, unavailable localization, focused
  policy tests and 4px HelpBox spacing. These remain uncommitted, alongside beta
  metadata/docs. The live user's project does not receive the beta metadata.
- Local ZIP candidate: com.lumakroma.sps2-setup-assistant-1.2.0-beta.1.zip,
  89 entries, SHA256 3d1e489bbda3c31beb7845630c62cb2b64cbf0d230df1d881ac564b523602591.
  Two fresh builds matched; build script checked every included source byte.
- User PASS was reported after latest UI/spacing, lilToon 2.3.4 and Gesture Manager
  3.9.9 installation in Unity 2022.3.22f1 / SDK 3.10.4 / SPSNDMF 1.1430.0 / MA 1.18.1 /
  NDMF 1.14.3. MANUKA was observed during manual use. Do not infer exhaustive
  avatar, network, mode, scene persistence or VRC client coverage from that report.
- Pending decision: approve exact version, existing public listing as beta destination,
  publish assets ZIP/package.json only, and whether a separate beta installer is wanted.
  Current stable VPAI remains untouched and excludes prereleases. No beta installer is proposed.
- Pending license confirmation: dependency declares VRCFury Personal / VRCA-VRCW;
  its commercial text restricts modified/derivative versions and directing users to
  them. Confirm intended commercial/noncommercial distribution/use and relevant
  rightsholder authorization. No legal clearance is claimed from the beta label.
- Check authenticated drafts immediately before draft creation. Public inventory
  contains no beta, but unauthenticated reads cannot exclude private drafts.
- Recheck deployment policy/ref: current main package metadata is 1.0.2, older than
  public stable 1.1.1. Release at exact reviewed Issue branch commit; Pages listing
  through reviewed main according to existing policy. Preserve all prior listing
  versions and the stable ZIP SHA; set prerelease=true and make_latest=false.
- Minimum future validation: final committed diff review and final ZIP native import,
  NDMF new setup/default, existing Fury load/Apply and family guard. Repeat focused
  tests only if executable code changes or final-source checks identify a concern.
  Do not repeat SDK/Controller builds just for metadata/docs.

See RELEASE_120_BETA1_PUBLICATION_REVIEW.md for actual license evidence, final assets and concrete approval scope.
