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
- JSON形式のみを出力すること。

[JSON STRUCTURE]
{{
  ""titles"": [""高CTRタイトル1"", ""高CTRタイトル2"", ""高CTRタイトル3""],
  ""thumbnail_texts"": [""強い文言1"", ""強い文言2""],
  ""tags"": [""タグ1"", ""タグ2"", ""タグ3""],
  ""description"": ""SEO最適化された説明文"",
  ""hook"": ""冒頭5秒の強烈な一言"",
  ""script_full"": ""自然な話し言葉の全文（{duration}秒分）"",
  ""scenes"": [
    {{
      ""id"": 1,
      ""narration"": ""シーンのナレーション"",
      ""visual_prompt"": ""English prompt for high quality AI image"",
      ""duration_seconds"": 5
    }}
  ],
  ""srt"": ""1\n00:00:00,000 --> 00:00:05,000\n字幕テキスト..."",
  ""risk_check"": ""炎上リスクや誇張の有無""
}}

[INPUT]
{inputUrlOrText}
";

            V2GenerationResult? finalResult = null;
            
            // JSON壊れ対策：最大3回リトライ
            for (int i = 0; i < 3; i++)
            {
                string response = await _localAI.GenerateResponseAsync(prompt);
                string cleanedJson = ExtractJson(response);

                try {
                    finalResult = JsonConvert.DeserializeObject<V2GenerationResult>(cleanedJson);
                    if (finalResult != null && finalResult.Scenes != null && finalResult.Scenes.Count > 0)
                    {
                        break; // 成功
                    }
                } catch (Exception ex) {
                    if (i == 2) throw new Exception($"AI出力の解析に失敗しました(3回試行): {ex.Message}\n内容: {cleanedJson}");
                    prompt += "\n\n[WARNING] 前回の出力はJSONとして不正でした。必ず正しいJSONのみを出力してください。";
                }
            }

            return finalResult!;
        }

        private string ExtractJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            // Find the first '{' and the last '}' to extract the core JSON block
            int start = input.IndexOf("{");
            int end = input.LastIndexOf("}");

            if (start != -1 && end != -1 && end > start)
            {
                return input.Substring(start, end - start + 1).Trim();
            }
            
            return input.Trim();
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
