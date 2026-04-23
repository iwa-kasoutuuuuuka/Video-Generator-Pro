# 取扱説明書 (User Manual) - Shorts Video Generator Pro

## 1. 導入手順
### 1.1 前提条件
- Windows 10 または 11 がインストールされていること。
- .NET 10 ランタイムまたは SDK がインストールされていること。
    - ダウンロード: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### 1.2 外部バイナリの配置 (FFmpeg)
動画の書き出しには FFmpeg が必要です。
1. [ffmpeg.org](https://ffmpeg.org/download.html) または [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) から Windows 用バイナリをダウンロードします。
2. 解凍した `ffmpeg.exe` を本ソフトの `bin/` フォルダ内に配置してください。

### 1.3 AIモデルの準備
著作権およびファイルサイズの都合上、AIモデルは同梱されていません。以下のサイトから各モデルをダウンロードし、`Models/` フォルダに配置してください。
- **ダウンロード先**: [Hugging Face](https://huggingface.co/)
- **推奨ファイル**:
    - LLM: `phi-3-mini-4k-instruct-q4.gguf`
    - SD: `v1-5-pruned-emaonly.safetensors`

## 2. 基本的な使い方
1. **アプリの起動**: `ShortsGeneratorApp.exe` (または `dotnet run`) を実行します。
2. **設定**: 画面左側の「Local AI Settings」で、使用するLLMと画像生成モデルを選択します。
3. **記事の入力**: 「Script Input」エリアに、動画の元となるブログ記事等のテキストを貼り付けます。
4. **生成**: 「生成を開始する」ボタンをクリックします。AIがスクリプトと各シーンの構成案を自動作成します。
5. **動画の書き出し**: 生成されたバリエーションカードの「動画を書き出す」ボタンをクリックします。デスクトップに `.mp4` ファイルが出力されます。

## 3. よくある質問 (FAQ)
- **Q: 生成が遅い。**
  - A: スペック不足の場合は「Gemma-2-2B」などのより軽量なモデルを選択してください。
- **Q: オフラインでも使えますか？**
  - A: はい。初回起動時に必要なモデルファイルさえ揃っていれば、以降は完全にインターネットなしで動作します。
- **Q: 商用利用は可能ですか？**
  - A: 本ソフト自体は商用利用可能ですが、使用するAIモデル（SDXL Turbo等）によっては別途規約がある場合があります。標準の SD 1.5 や Phi-3 は商用利用が許可されています。

## 4. トラブルシューティング
- **ffmpeg.exe が見つからない**: `bin/` フォルダのパスを再確認してください。
- **メモリ不足エラー**: 他の重いアプリ（ブラウザ等）を閉じてから実行してください。
