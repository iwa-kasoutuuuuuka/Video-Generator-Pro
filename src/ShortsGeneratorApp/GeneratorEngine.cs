using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ShortsGeneratorApp
{
    public class GeneratorEngine
    {
        private readonly LocalGeneratorEngine _localAI;

        public GeneratorEngine(LocalGeneratorEngine localAI)
        {
            _localAI = localAI;
        }

        public async Task<V2GenerationResult> GenerateV2Async(string inputUrlOrText, int duration, string style)
        {
            // 視聴維持率・CTR最適化プロンプト (v2.0 / v5.0 logic)
            string prompt = $@"
[ROLE]
あなたは「YouTube/Shorts動画の視聴維持率・CTR最適化AI」です。視聴者が最後まで飽きずに視聴し、クリックしたくなる台本とメタデータを生成してください。

[TASK]
入力されたコンテンツを分析し、YouTubeアルゴリズム（満足度・維持率）に最適化された出力を生成せよ。

[RULES]
- 導入は最初の5秒で視聴者を引き込む「強力なフック」を設計すること。
- 内容は中学生でも理解できる平易かつ、感情を動かすテンポの良い表現にする。
- 出力は必ず以下のJSON構造に従い、純粋なJSONのみを返せ。
- 言語は日本語を使用すること。ただし `visual_prompt` は画像生成AI（Stable Diffusion）向けに詳細な英語で記述せよ。
- **解説、前置き、Markdownの枠、### などの記号は一切禁止する。**

[SUCCESS EXAMPLE]
{{
  ""titles"": [""驚愕の真実！"", ""誰も知らない裏技"", ""3分でわかる解説""],
  ""thumbnail_texts"": [""ヤバイ"", ""禁断の手法""],
  ""tags"": [""解説"", ""豆知識""],
  ""description"": ""今回の動画では..."",
  ""hook"": ""ねぇ、これ知ってた？"",
  ""script_full"": ""こんにちは！今日は..."",
  ""scenes"": [
    {{
      ""id"": 1,
      ""narration"": ""こんにちは！今日は驚きの事実をお伝えします。"",
      ""visual_prompt"": ""A surprised young woman pointing at a glowing mysterious smartphone, cinematic lighting, 8k, detailed"",
      ""duration_seconds"": 5
    }}
  ],
  ""srt"": ""1\n00:00:00,000 --> 00:00:05,000\nこんにちは！今日は驚きの事実をお伝えします。"",
  ""risk_check"": ""なし""
}}

[STRICT RULE]
あなたはJSON生成機械です。`{{` で始まり `}}` で終わるデータ以外を1文字でも出力したらエラーとなります。

[INPUT DATA START]
{inputUrlOrText}
[INPUT DATA END]
";

            V2GenerationResult? finalResult = null;
            
            // JSON壊れ対策：最大3回リトライ
            for (int i = 0; i < 3; i++)
            {
                string response = await _localAI.GenerateResponseAsync(prompt);
                string cleanedJson = ExtractJson(response);

                if (string.IsNullOrEmpty(cleanedJson) || !cleanedJson.StartsWith("{"))
                {
                    if (i == 2) throw new Exception($"AIが有効なJSONを生成できませんでした。内容: {response}");
                    prompt += "\n\n[ERROR] 前回の出力にはJSONが含まれていませんでした。解説を省き、{ } で囲まれたJSONデータのみを出力してください。";
                    continue;
                }

                try {
                    finalResult = JsonConvert.DeserializeObject<V2GenerationResult>(cleanedJson);
                    if (finalResult != null && finalResult.Scenes != null && finalResult.Scenes.Count > 0)
                    {
                        break; // 成功
                    }
                } catch (Exception ex) {
                    if (i == 2) throw new Exception($"AI出力の解析に失敗しました(3回試行): {ex.Message}\n内容: {cleanedJson}");
                    prompt += $"\n\n[WARNING] 前回の出力はJSONとして不正でした({ex.Message})。構文エラーを修正し、正しいJSONのみを出力してください。";
                }
            }

            return finalResult!;
        }

        private string ExtractJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            // 1. Try to find JSON inside markdown code blocks ```json ... ``` or ``` ... ```
            var match = Regex.Match(input, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
            if (match.Success) return match.Groups[1].Value.Trim();

            // 2. Find the first '{' and the last '}' to extract the core JSON block
            int start = input.IndexOf("{");
            int end = input.LastIndexOf("}");

            if (start != -1 && end != -1 && end > start)
            {
                return input.Substring(start, end - start + 1).Trim();
            }
            
            return ""; // JSON not found
        }
    }

    public class V2GenerationResult
    {
        [JsonProperty("titles")]
        public List<string> Titles { get; set; } = new();

        [JsonProperty("thumbnail_texts")]
        public List<string> ThumbnailTexts { get; set; } = new();

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("hook")]
        public string Hook { get; set; } = "";

        [JsonProperty("script_full")]
        public string ScriptFull { get; set; } = "";

        [JsonProperty("scenes")]
        public List<V2Scene> Scenes { get; set; } = new();

        [JsonProperty("srt")]
        public string Srt { get; set; } = "";

        [JsonProperty("risk_check")]
        public string RiskCheck { get; set; } = "";
    }

    public class V2Scene
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("narration")]
        public string Narration { get; set; } = "";
        [JsonProperty("visual_prompt")]
        public string VisualPrompt { get; set; } = "";
        [JsonProperty("duration_seconds")]
        public double DurationSeconds { get; set; }
    }
}
