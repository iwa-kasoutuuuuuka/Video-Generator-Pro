using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ShortsGeneratorApp
{
    public class ShortsGeneratorEngine
    {
        public const string PromptTemplate = @"
[TASK]
あなたは、日本のバイラルコンテンツに特化した、オフライン最適化・高維持率のYouTube Shorts & TikTok自動制作エンジンです。

[CONFIG]
- Platform: {{PLATFORM}}
- Orientation: {{ORIENTATION}}
- Style: {{STYLE}}
- Target Duration: {{DURATION}} seconds
- Target Total Characters: {{TARGET_CHARS}} (Range: {{MIN_CHARS}} - {{MAX_CHARS}})

[OUTPUT JSON TEMPLATE]
{
  ""platform"": ""{{PLATFORM}}"",
  ""orientation"": ""{{ORIENTATION}}"",
  ""target_duration"": {{DURATION}},
  ""variations"": [
    {
      ""variation_id"": ""V1"",
      ""scenes"": [
        {
          ""id"": 1,
          ""narration"": ""(スタイルに合わせた導入ナレーション)"",
          ""telop_text"": ""(短く強いテロップ)"",
          ""visual_prompt"": ""(Detailed English prompt for image generation)""
        }
      ]
    }
  ]
}

[INPUT]
{{BLOG_TEXT}}
";

        private LocalGeneratorEngine _localAI;

        public ShortsGeneratorEngine(LocalGeneratorEngine localAI)
        {
            _localAI = localAI;
        }

        public async Task<GenerationResult> GenerateAsync(string platform, string blogText, int duration, string orientation, int targetChars, string style = "標準 (Standard)")
        {
            int minChars = (int)(targetChars * 0.9);
            int maxChars = (int)(targetChars * 1.1);

            string styleInstruction = "";
            if (style.Contains("煽り")) styleInstruction = "視聴者の注意を強く引く、ショッキングでエネルギッシュな口調";
            else if (style.Contains("教育")) styleInstruction = "丁寧で分かりやすく、信頼感のある落ち着いた口調";
            else styleInstruction = "聞き取りやすく、自然で親しみやすい標準的な口調";

            string prompt = PromptTemplate
                .Replace("{{PLATFORM}}", platform)
                .Replace("{{ORIENTATION}}", orientation)
                .Replace("{{STYLE}}", styleInstruction)
                .Replace("{{DURATION}}", duration.ToString())
                .Replace("{{TARGET_CHARS}}", targetChars.ToString())
                .Replace("{{MIN_CHARS}}", minChars.ToString())
                .Replace("{{MAX_CHARS}}", maxChars.ToString())
                .Replace("{{BLOG_TEXT}}", blogText);

            string jsonResponse = await _localAI.GenerateResponseAsync(prompt);
            
            // Debug: Save raw response to file
            try {
                System.IO.File.AppendAllText("raw_ai_output.txt", $"\n--- {DateTime.Now} ---\n{jsonResponse}\n");
            } catch { }

            // AI output might contain markdown or extra text, try to extract JSON
            string cleanedJson = ExtractJson(jsonResponse);

            try {
                var result = JsonConvert.DeserializeObject<GenerationResult>(cleanedJson) ?? throw new Exception("AI出力の解析に失敗しました。");
                
                // Post-processing: Fix redundant or overly long telops
                foreach (var v in result.Variations)
                {
                    foreach (var s in v.Scenes)
                    {
                        if (string.IsNullOrEmpty(s.TelopText) || s.TelopText == s.Narration)
                        {
                            // If identical or empty, try to summarize or truncate
                            if (s.Narration.Length > 15)
                                s.TelopText = s.Narration.Substring(0, 15) + "...";
                            else
                                s.TelopText = s.Narration;
                        }
                        
                        // Limit to 25 chars for safety if still too long
                        if (s.TelopText.Length > 25)
                            s.TelopText = s.TelopText.Substring(0, 22) + "...";
                    }
                }
                
                return result;
            }
            catch (Exception ex) {
                // If it fails, show a snippet of the problematic JSON for debugging
                string snippet = cleanedJson.Length > 200 ? cleanedJson.Substring(0, 200) + "..." : cleanedJson;
                throw new Exception($"AIが生成したシナリオが正しい形式ではありませんでした。\n\n解析エラー: {ex.Message}\n内容抜粋: {snippet}");
            }
        }

        private string ExtractJson(string input)
        {
            // Remove markdown code blocks if present
            string cleaned = input;
            if (input.Contains("```json")) {
                int startJson = input.IndexOf("```json") + 7;
                int endJson = input.IndexOf("```", startJson);
                if (endJson != -1) cleaned = input.Substring(startJson, endJson - startJson);
            }
            
            int start = cleaned.IndexOf("{");
            int end = cleaned.LastIndexOf("}");
            if (start != -1 && end != -1 && end > start) {
                return RepairJson(cleaned.Substring(start, end - start + 1).Trim());
            }

            return RepairJson(cleaned.Trim());
        }

        private string RepairJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            // Remove trailing commas before closing braces/brackets (common AI error)
            string repaired = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([\]}])", "$1");
            
            return repaired; 
        }

        public async Task<Variation> RefineTextAsync(Variation v)
        {
            string currentScript = JsonConvert.SerializeObject(v, Formatting.Indented);
            string prompt = $@"
[TASK]
以下の動画台本（JSON）を、より自然で魅力的な内容に『ブラッシュアップ』してください。
特に、ナレーションの言い回しをプロフェッショナルにし、テロップをよりキャッチーにしてください。
JSONの構造は変えず、内容だけを更新してください。

[RULES]
- ナレーションは聞き取りやすく、印象的な話し言葉にする。
- テロップは短く！（15字以内）、ナレーションの要素や補足を強調する。
- 画像プロンプト（visual_prompt）は現在のものを維持するか、より詳細に改善する。

[CURRENT JSON]
{currentScript}
";

            string jsonResponse = await _localAI.GenerateResponseAsync(prompt);
            string cleanedJson = ExtractJson(jsonResponse);

            try {
                return JsonConvert.DeserializeObject<Variation>(cleanedJson) ?? v;
            } catch {
                return v;
            }
        }
    }

    // --- Data Models ---

    public class GenerationResult
    {
        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("orientation")]
        public string Orientation { get; set; }

        [JsonProperty("target_duration")]
        public double TargetDuration { get; set; }

        [JsonProperty("variations")]
        public List<Variation> Variations { get; set; } = new List<Variation>();
    }

    public class Variation
    {
        [JsonProperty("variation_id")]
        public string VariationId { get; set; }

        [JsonProperty("hook_type")]
        public string HookType { get; set; }

        [JsonProperty("total_duration")]
        public double TotalDuration { get; set; }

        [JsonProperty("scenes")]
        public List<Scene> Scenes { get; set; } = new List<Scene>();
    }

    public class Scene
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("narration")]
        public string Narration { get; set; }

        [JsonProperty("telop_text")]
        public string TelopText { get; set; }

        [JsonProperty("visual_prompt")]
        public string VisualPrompt { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("telop")]
        public Telop Telop { get; set; }

        [JsonProperty("music_sync")]
        public string MusicSync { get; set; }

        public string GetTelopText() => TelopText ?? Telop?.Text ?? "";
    }

    public class Telop
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("emphasis")]
        public bool Emphasis { get; set; }

        [JsonProperty("style")]
        public string Style { get; set; }
    }

    public class Voice
    {
        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("pitch")]
        public string Pitch { get; set; }

        [JsonProperty("emotion")]
        public string Emotion { get; set; }
    }

    public class GlobalConfig
    {
        [JsonProperty("resolution")]
        public string Resolution { get; set; }

        [JsonProperty("bgm_vibe")]
        public string BgmVibe { get; set; }

        [JsonProperty("total_duration")]
        public double TotalDuration { get; set; }
    }
}
