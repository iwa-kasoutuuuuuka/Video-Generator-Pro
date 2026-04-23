# 技術仕様書 (Technical Specifications) - Shorts Video Generator Pro

## 1. 概要
本ソフトウェアは、Windows環境で動作する完全オフライン・ローカル完結型のショート動画生成支援ツールです。AI（LLMおよび画像生成モデル）を活用し、ブログ記事等のテキストから動画スクリプトの作成、音声合成、画像生成、および動画エンコードを一貫して行います。

## 2. システムアーキテクチャ
- **プラットフォーム**: .NET 10 (WPF)
- **開発言語**: C# 13
- **動作環境**: Windows 10/11 (64bit)
- **推奨ハードウェア**: 
    - CPU: 4コア以上
    - RAM: 8GB以上
    - GPU: NVIDIA GTX 1650 (VRAM 4GB) 以上推奨 (CPUのみでも動作可能)

## 3. 使用技術・ライブラリ
### 3.1 ソフトウェア・コンポーネント
- **FFmpeg**: 動画・音声のエンコードおよびミキシング。
    - 公式サイト: [https://ffmpeg.org/](https://ffmpeg.org/)
- **LLamaSharp**: ローカルLLM (GGUF形式) の C# 推論エンジン。
    - リポジトリ: [https://github.com/SciSharp/LLamaSharp](https://github.com/SciSharp/LLamaSharp)
- **FFMpegCore**: .NET用 FFmpeg ラッパー。
- **Newtonsoft.Json**: JSONデータのシリアライズ/デシリアライズ。

### 3.2 AIモデル
- **LLM (Text Generation)**:
    - Phi-3 Mini (Microsoft) [MIT License]
    - Gemma-2-2B (Google) [Gemma Terms of Use]
- **Image Generation**:
    - Stable Diffusion v1.5 [CreativeML Open RAIL-M]
- **TTS (Text-to-Speech)**:
    - Windows System Speech (SAPI5)

## 4. フォルダ構成 (GitHub用)
```
ShortsGeneratorApp/
├── bin/                 # 実行バイナリ、FFmpeg.exe
├── docs/                # ドキュメント (本資料、取扱説明書)
├── Models/              # AIモデル格納用 (ユーザーにて配置)
├── src/                 # ソースコード
│   └── ShortsGeneratorApp/
├── ShortsGeneratorApp.sln
├── .gitignore           # Modelsやbin/等のバイナリを除外
└── README.md            # プロジェクト概要と導入手順
```

## 5. ライセンス
本プロジェクトのソースコードは MIT ライセンスの下で提供されます。
使用している外部ライブラリおよびAIモデルのライセンスについては、各提供元の規約を遵守してください。
