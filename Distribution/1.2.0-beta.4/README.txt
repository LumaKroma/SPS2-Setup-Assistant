SPS2 Setup Assistant 1.2.0-beta.4 系列別 VPM インストーラー

1.2.0-beta.4 向けです。共通 SPS2 と依存は公式配布元から取得します。
Fury / NDMF のどちらか一つの ZIP を選び、unitypackage を対象 Unity 2022.3
VRChat Avatar プロジェクトにインポートし、VPAI の確認画面を読んでください。
依存本体・アバター・scene は同梱しません。通信と公式配布からの取得が必要です。

Fury版: 共通SPS2 1.2.0-beta.4 + VRCFury >=1.0.0 + lilToon >=1.0.0。
NDMF版: 共通SPS2 1.2.0-beta.4 + SPS for NDMF 1.1430.0 + MA >=1.18.1 <2.0.0
       + NDMF >=1.11.0 <2.0.0 + lilToon >=1.0.0。Fury/Stub/Migratorは要求しません。
SDKは共通SPS2のVPM必須依存 >=3.10.4 <4.0.0 として解決されます。
表示用lilToonを両インストーラーの取得対象に含めます。

Unity: Tools > LumaKroma > SPS2 Setup Assistant を開きます。
使える系列が一つなら新規生成で自動選択され、両方あれば希望の系列を選べます。
インストーラーは保存済み設定・生成物の系列を書き換えません。
既存依存は指定範囲に応じて解決されるため、版の更新が提案される場合があります。
両インストーラーは同じVPAI一時GUIDを使うため同時にインポートしないでください。

導入前にプロジェクト全体を外部へバックアップしてください。
旧Assets版の Assets/LumaKroma/SPS2SetupAssistant は標準VPM移行で削除対象となり、
中のユーザー追加ファイルも対象になり得ます。先にフォルダを確認してください。
Assets/SPS2Settings はその削除対象ではありません。既に導入済みの場合の早期終了は
重複した旧Assets版を後から片付ける保証にはなりません。

各依存は独自ライセンスです。SPS2本体のMITで再許諾されません。
SPSNDMFを使う商用アバター制作・販売・寄付付きサービス等は、改変版への依存・
利用案内に関する条件を確認してください。非同梱は商用許諾の保証ではありません。
https://github.com/niriziki/VRCFury/blob/ndmf/LICENSE.md

VPAI 1.1.6: https://github.com/anatawa12/VPMPackageAutoInstaller/releases/tag/v1.1.6
同梱VPAI-LICENSE.txt / VPAI-LICENSE-semver.net.txtを保持してください。

更新後は各SPS基盤の標準メニュー・初期値・保存設定を使用します。
Local Onlyは基盤側のStealth Modeを使用し、メニュー言語はソケット名のみに適用されます。
既存の表示名を反映するには「置き換えずに変更を反映」を実行して保存してください。
アバターの再ビルド・再アップロード後に、メニューと各モードの状態を確認してください。
NDMFのObjectアクションを使う場合は参照先とON/OFFの指定も確認してください。
VRC実機のGestureと最終material/prefab動作は未確認です。
このインストーラーは公開されたVPM listingから取得します。未公開候補はlistingに存在するまで取得できません。
