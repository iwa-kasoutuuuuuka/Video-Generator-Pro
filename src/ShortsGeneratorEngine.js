export const PROMPT_TEMPLATE = `
ROLE
You are an offline-optimized, high-retention YouTube Shorts & TikTok auto-production engine specialized in Japanese viral short-form content with built-in AI music generation, MP3 export, and metadata logging.

SETTING
Platform: {{PLATFORM}} (YouTube_Shorts or TikTok)
Input: {{BLOG_TEXT}}

PLATFORM LOGIC
IF YouTube_Shorts:
  Focus: Educational value + Maximum retention
  Target length: 15〜30 seconds (ideal 18-25s)
  Ending: Infinite Loop

IF TikTok:
  Focus: Strong Pattern Interrupt + Emotional spike + Virality
  Target length: 12〜25 seconds (ideal 15-20s)
  Ending: Community Drive

GOAL
Generate exactly 2 HIGH-RETENTION variations in STRICT JSON format.
各variationに最適なAI音楽を自動生成するための詳細promptを作成し、MP3出力とメタ情報テキストファイル生成をサポートする。

HARD CONSTRAINTS (絶対厳守)
- Output ONLY valid JSON. No preamble, no explanation, no markdown.
- Narration: 最大18文字以内（句読点含む）。
- Forbidden phrases完全排除（こんにちは, いかがでしたか, 実は, 皆さん, などの導入・結びフレーズ）。
- Scene Count: 4〜6 scenes。
- BGM: 各variationごとに総時間に完全に一致したAI音楽生成promptを作成。MP3出力前提でロイヤリティフリー・商用利用OKを明記。
- Metadata: 各variationごとに音楽生成時の詳細情報をテキストファイルとして出力するための内容を準備。

SCENE STRUCTURE (各シーン必須項目)
- id, narration, telop, visual, transition, voice, pause_after, duration, music_sync

OUTPUT FORMAT (STRICT JSON)
{
  "platform": "{{PLATFORM}}",
  "variations": [
    {
      "variation_id": "A",
      "hook_type": "...",
      "bgm": { "auto_generation_prompt": "...", "metadata_txt_content": "..." },
      "scenes": [ ... ]
    }
  ]
}
`;

export const generateScript = async (platform, blogText) => {
  // In a real app, this would call an API (Gemini, etc.)
  // For demonstration, we will return a simulated high-quality response.
  console.log("Generating for:", platform);
  
  // Simulated delay
  await new Promise(resolve => setTimeout(resolve, 2000));

  // Dummy response based on a common topic if text is empty or generic
  return {
    "platform": platform,
    "target_duration_range": platform === "YouTube_Shorts" ? "15-30秒" : "12-25秒",
    "variations": [
      {
        "variation_id": "A",
        "hook_type": "Surprise",
        "loop_or_cta_logic": platform === "YouTube_Shorts" ? "最後の一言をフックの『それ、逆効果です』に繋げる" : "コメント欄で意見を聞く",
        "total_duration": 18.5,
        "scenes": [
          {
            "id": 1,
            "narration": "それ、逆効果です。",
            "telop": { "text": "逆効果", "emphasis": true, "style": "shake" },
            "visual": "Shocked face with red X mark",
            "transition": "zoom shock",
            "voice": { "speed": 1.3, "pitch": "high", "emotion": "warning" },
            "pause_after": 0.1,
            "duration": 2.5,
            "music_sync": "drop_impact"
          },
          {
            "id": 2,
            "narration": "良かれと思ってやる行動が、実は一番危険。",
            "telop": { "text": "一番危険な行動", "emphasis": true, "style": "red" },
            "visual": "Split screen of right vs wrong action",
            "transition": "fast swipe",
            "voice": { "speed": 1.35, "pitch": "normal", "emotion": "urgent" },
            "pause_after": 0.2,
            "duration": 4.5,
            "music_sync": "main_beat"
          },
          {
            "id": 3,
            "narration": "正しい方法は、まず深呼吸すること。",
            "telop": { "text": "深呼吸が正解", "emphasis": false, "style": "pop" },
            "visual": "Calm person breathing deeply",
            "transition": "quick cut",
            "voice": { "speed": 1.25, "pitch": "normal", "emotion": "relief" },
            "pause_after": 0.1,
            "duration": 3.5,
            "music_sync": "steady_pace"
          },
          {
            "id": 4,
            "narration": "保存して、明日から試してみて。",
            "telop": { "text": "今すぐ保存", "emphasis": true, "style": "big" },
            "visual": "Animated save button icon",
            "transition": "dramatic pause",
            "voice": { "speed": 1.3, "pitch": "normal", "emotion": "relief" },
            "pause_after": 0.3,
            "duration": 4.0,
            "music_sync": "resolution"
          },
          {
            "id": 5,
            "narration": "忘れないうちにチェックしてね。",
            "telop": { "text": "忘れないで", "emphasis": true, "style": "shake" },
            "visual": "Checklist animation",
            "transition": "zoom shock",
            "voice": { "speed": 1.4, "pitch": "high", "emotion": "urgent" },
            "pause_after": 0.1,
            "duration": 4.0,
            "music_sync": "loop_point"
          }
        ],
        "bgm": {
          "auto_generation_prompt": "18.5 second high-energy electronic background music, Japanese viral short style, intense drop at hook, steady fast beat for body, seamless loop, high-quality instrumental, commercial use ok.",
          "vibe": "High-energy Electronic",
          "target_length_seconds": 18.5,
          "recommended_tool": "Soundraw",
          "mp3_filename_suggestion": "variation_A_bgm.mp3",
          "metadata_txt_content": "=== AI生成音楽メタ情報 ===\n生成日時: 2026-04-23\nVariation: A\n総時間: 18.5秒\nBGMスタイル: High-energy Electronic"
        }
      },
      {
        "variation_id": "B",
        "hook_type": "Tips",
        "loop_or_cta_logic": platform === "YouTube_Shorts" ? "最後の一言をフックに繋げる" : "フォローを促す",
        "total_duration": 20.0,
        "scenes": [
          {
            "id": 1,
            "narration": "知らないと損します。",
            "telop": { "text": "知らないと損", "emphasis": true, "style": "red" },
            "visual": "Person counting money with worried face",
            "transition": "instant shock zoom",
            "voice": { "speed": 1.3, "pitch": "high", "emotion": "warning" },
            "pause_after": 0.1,
            "duration": 3.0,
            "music_sync": "intro_build"
          },
          {
            "id": 2,
            "narration": "実はこれだけで、効率が3倍上がります。",
            "telop": { "text": "効率3倍UP", "emphasis": true, "style": "big" },
            "visual": "Graph showing sharp increase",
            "transition": "fast swipe",
            "voice": { "speed": 1.35, "pitch": "normal", "emotion": "surprise" },
            "pause_after": 0.2,
            "duration": 4.5,
            "music_sync": "drop_impact"
          },
          {
            "id": 3,
            "narration": "ポイントは、夜寝る前の1分だけ。",
            "telop": { "text": "寝る前1分", "emphasis": true, "style": "pop" },
            "visual": "Clock showing 11:00 PM",
            "transition": "quick cut",
            "voice": { "speed": 1.25, "pitch": "normal", "emotion": "relief" },
            "pause_after": 0.1,
            "duration": 4.5,
            "music_sync": "steady_pace"
          },
          {
            "id": 4,
            "narration": "具体的な方法はコメント欄をチェック。",
            "telop": { "text": "コメ欄をチェック", "emphasis": true, "style": "shake" },
            "visual": "Arrow pointing down to comment section",
            "transition": "zoom shock",
            "voice": { "speed": 1.4, "pitch": "high", "emotion": "urgent" },
            "pause_after": 0.2,
            "duration": 4.0,
            "music_sync": "climax"
          },
          {
            "id": 5,
            "narration": "もっと知りたい人はフォローしてね。",
            "telop": { "text": "今すぐフォロー", "emphasis": true, "style": "big" },
            "visual": "Animated follow button",
            "transition": "fast swipe",
            "voice": { "speed": 1.3, "pitch": "normal", "emotion": "relief" },
            "pause_after": 0.3,
            "duration": 4.0,
            "music_sync": "resolution"
          }
        ],
        "bgm": {
          "auto_generation_prompt": "20.0 second chill lo-fi beat with optimistic vibe, Japanese productivity tips style, soft build-up, clear drop at climax, royalty-free, MP3 export ready.",
          "vibe": "Chill Lo-fi",
          "target_length_seconds": 20.0,
          "recommended_tool": "Suno",
          "mp3_filename_suggestion": "variation_B_bgm.mp3",
          "metadata_txt_content": "=== AI生成音楽メタ情報 ===\n生成日時: 2026-04-23\nVariation: B\n総時間: 20.0秒\nBGMスタイル: Chill Lo-fi"
        }
      }

    ]
  };
};
