# NewsToYouTubeSkill v5.0 (アルゴリズム最適化版)

## 概要
ニュース記事URLから、YouTubeアルゴリズム（CTR・視聴維持率）に最適化された動画を完全自動生成するスキル。

## コア・ロジック (C++ v5.0)
- **AIプロンプト**: CTR重視タイトル、強力なフック、視聴維持率を高めるナレーション構成。
- **リトライ制御**: JSON壊れを検知し、最大3回までプロンプトを補正して再試行。
- **映像演出**: FFmpegの `zoompan` フィルタによる動的な演出。
- **音響最適化**: `loudnorm` によるYouTube標準音量への自動変換。
- **字幕合成**: SRT形式の字幕を自動生成し、動画に焼き付け。

## 技術スタック
- **LLM**: llama.cpp (llama-cli.exe) + アルゴリズム最適化プロンプト
- **TTS**: piper.exe
- **Render**: ffmpeg (zoompan, subtitles, loudnorm)
- **Tools**: curl (Web抽出), powershell (JSON抽出・修復)

## プロンプト定義
```text
あなたは「YouTubeニュース動画の視聴維持率・CTR最適化AI」です。
KPI: CTR、視聴維持率、コメント誘導率
ルール: 
1. 本文抽出
2. 3〜5点圧縮
3. バズ分析
4. 強力フック設計
5. 構成（導入→展開→まとめ→CTA）
6. 短文・テンポ重視
```
