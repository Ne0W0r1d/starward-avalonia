[English](../README.md) | [简体中文](./README.zh-CN.md) | [Tiếng Việt](./README.vi-VN.md) | **日本語** | [ภาษาไทย](./README.th-TH.md) | [Русский](./README.ru-RU.md)


# Starward-Avalonia

> **Starward** は『崩壊：スターレイル』のキャッチコピー「May This Journey Lead Us **Starward**（この旅が私たちを星へと導くように）」に由来しており、アプリ名としてとてもふさわしいものです。


> [!WARNING]
> 本プロジェクトは Starward の下流フォークです。Avalonia へ移植し、Linux ユーザー向けに提供する予定です。


Starward-Avalonia は、HoYoPlay（miHoYo ランチャー）の不足を補うために開発されたオープンソースのサードパーティ製ランチャーです。HoYoverse のすべての PC ゲームに対応し、Linux PC および Steam Deck で公式ランチャーを完全に置き換えることを目指しています。ランチャーの基本機能に加えて、個人のニーズに応じた追加機能も取り入れています。例えば：

- プレイ時間の記録
- ゲームアカウントの切り替え
- ゲームのスクリーンショットの閲覧
- ガチャ履歴の保存

その他の機能はご自身でお試しください……


## インストール

まず、お使いのデバイスが以下の要件を満たしている必要があります：

- .NET 10 をインストールできる Linux ディストリビューション
- WebKit2GTK がインストールされていること


次に、[GitHub Release](https://github.com/Ne0W0r1d/Starward-avalonia/releases) からお使いの CPU アーキテクチャ向けのパッケージをダウンロードします。解凍したら、インストールするかそのまま実行し、画面の指示に従ってください。


## 開発

[.NET 10 SDK](https://dotnet.microsoft.com/download) をインストールしてください。その後、リポジトリのルートでビルドと実行を行います：

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia
```

コードの編集には VS Code / VSCodium（C# 拡張機能）または JetBrains Rider を使用できます。


## 寄付

開発は簡単なものではありません。Starward が役に立つと思っていただけたら：

- https://donate.scighost.com でアップストリームを支援できます。
- または https://0w0.red/sponsor/ で私のトークン利用を支援できます。





## スクリーンショット
