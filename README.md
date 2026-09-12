# SPS2 Setup Assistant

VRChatアバターへのSPS2セットアップを補助するUnity Editorツール

使用する部位を選ぶだけでソケットの配置やメニューの生成をまとめて行えます。

## 導入

先に[VRCFury](https://vcc.vrcfury.com/)と[lilToon](https://lilxyzw.github.io/vpm-repos/vpm.json)のリポジトリをVCCまたはALCOMに登録し、導入先のアバタープロジェクトへ両パッケージを追加してください。

[こちらからリポジトリを追加](vcc://vpm/addRepo?url=https%3A%2F%2Flumakroma.github.io%2FSPS2-Setup-Assistant%2Findex.json)し、VCCまたはALCOMで追加を承認してください。

続いて導入先のアバタープロジェクトを開き、パッケージ一覧から **SPS2 Setup Assistant** を追加してください。

手動で追加する場合のリポジトリURL：`https://lumakroma.github.io/SPS2-Setup-Assistant/index.json`

## 使い方

一番シンプルな方法は、上部メニュー欄から `Tools > LumaKroma > SPS2 Setup Assistant` を開いて対象アバターを指定 → `プレハブを生成`。

生成前にプリセットや使用する部位、深度アクション、設定項目をカスタマイズできます。

設定の変更・再生成、テストプラグの配置もできます。

対象はHumanoidアバターのみです。自動配置の結果はアバターによって異なるため、生成後に位置や向きを確認してください。

## 機能

- プリセットを使ったセットアップ
- 使用する部位に応じたソケットの自動配置
- ソケットを操作するメニューの自動生成
- 深度アクションの設定
- 口と肛門をつなぐ貫通経路の生成
- Auto Mode・後方互換性・Local Onlyの設定
- 手動調整した位置を維持した設定の反映
- 設定に合わせた再生成
- 通常のテストプラグと、貫通確認用の長いテストプラグの配置

## 依存

- Unity 2022.3
- VRChat Avatars SDK
- VRCFury
- lilToon（テストプラグの表示用）
- Modular Avatar（任意・追従方式の追加）

## ライセンス

コード・ドキュメントおよび同梱のIcePopアセットはMITライセンスです。

本ツールは非公式の補助ツールです。
