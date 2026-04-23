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
入力文をもとに、{{PLATFORM}}向けの動画台本（JSON形式）を1つ作成してください。
視聴者の目を引き、最後まで飽きさせない構成にしてください。

[ルール]
1. ナレーション (narration): 
   - 視聴者に語りかけるような、自然で親しみやすい話し言葉にしてください。
   - 難しい漢字は避け、聞き取りやすい文章にしてください。
   - **全シーンの合計ナレーション文字数を {{MIN_CHARS}}文字 〜 {{MAX_CHARS}}文字 の範囲内に必ず収めてください。**
2. テロップ (telop_text):
   - 15文字以内の短くインパクトのある言葉にしてください。
   - **ナレーションの丸写しは厳禁です。** 内容を要約するか、補足するキャッチコピーにしてください。
3. 画像指示 (visual_prompt):
   - Stable Diffusionで使用可能な「具体的で高品質な英語のプロンプト」を作成してください。
   - 例: ""Cinematic shot of a futuristic city at sunset, 8k, highly detailed""
4. 構成:
   - 目標時間（{{DURATION}}秒）に合わせて、適切な数のシーンを作成してください（1シーン3〜5秒目安）。

[CONFIG]
- Platform: {{PLATFORM}}
- Orientation: {{ORIENTATION}}
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
          ""narration"": ""(視聴者の興味を引く導入ナレーション)"",
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


        public async Task<GenerationResult> GenerateAsync(string platform, string blogText, int duration, string orientation, int targetChars)
        {
            int minChars = (int)(targetChars * 0.9);
            int maxChars = (int)(targetChars * 1.1);

            string prompt = PromptTemplate
                .Replace("{{PLATFORM}}", platform)
                .Replace("{{ORIENTATION}}", orientation)
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
                        
                        // Limit to 20 chars for safety if still too long
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
            else if (input.Contains("```")) {
                int startJson = input.IndexOf("```") + 3;
                int endJson = input.IndexOf("```", startJson);
                if (endJson != -1) cleaned = input.Substring(startJson, endJson - startJson);
            }

            int start = cleaned.IndexOf("{");
            if (start == -1) return cleaned.Trim();

            // Find matching closing brace to avoid "Additional text" error
            int braceCount = 0;
            for (int i = start; i < cleaned.Length; i++)
            {
                if (cleaned[i] == '{') braceCount++;
                else if (cleaned[i] == '}') braceCount--;

                if (braceCount == 0)
                {
                    string json = cleaned.Substring(start, i - start + 1);
                    return RepairJson(json);
                }
            }

            return RepairJson(cleaned.Substring(start).Trim());
        }

        private string RepairJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            // Remove trailing commas before closing braces/brackets (common AI error)
            string repaired = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([\]}])", "$1");
            
            // Fix unquoted keys if they are simple words (common in some models)
            // repaired = System.Text.RegularExpressions.Regex.Replace(repaired, @"([{,]\s*)([a-zA-Z0-9_]+)\s*:", "$1\"$2\":");

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
- ナレーションは聞き取りやすく、魅力的な話し言葉にする。
- テロップは短く（15字以内）、ナレーションの要約や補足にする。
- 画像プロンプト（visual_prompt）は現在のものを維持するか、より詳細に改善する。

[CURRENT JSON]
{currentScript}
";

            string jsonResponse = await _localAI.GenerateResponseAsync(prompt);
            string cleanedJson = ExtractJson(jsonResponse);

            try {
                return JsonConvert.DeserializeObject<Variation>(cleanedJson) ?? v;
            }
            catch {
                return v; // Fallback to original
            }
        }
    }

    public class GenerationResult
    {
        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("orientation")]
        public string Orientation { get; set; }

        [JsonProperty("target_duration")]
        public double TargetDuration { get; set; }

        [JsonProperty("variations")]
        public List<Variation> Variations { get; set; }
    }

    public class Variation
    {
        [JsonProperty("variation_id")]
        public string VariationId { get; set; }

        [JsonProperty("hook_type")]
        public string HookType { get; set; }

        [JsonProperty("loop_or_cta_logic")]
        public string LoopOrCtaLogic { get; set; }

        [JsonProperty("total_duration")]
        public double TotalDuration { get; set; }

        [JsonProperty("bgm")]
        public BgmInfo Bgm { get; set; }

        [JsonProperty("scenes")]
        public List<Scene> Scenes { get; set; }
    }

    public class BgmInfo
    {
        [JsonProperty("auto_generation_prompt")]
        public string AutoGenerationPrompt { get; set; }

        [JsonProperty("vibe")]
        public string Vibe { get; set; }

        [JsonProperty("target_length_seconds")]
        public double TargetLengthSeconds { get; set; }

        [JsonProperty("recommended_tool")]
        public string RecommendedTool { get; set; }

        [JsonProperty("mp3_filename_suggestion")]
        public string Mp3FilenameSuggestion { get; set; }

        [JsonProperty("metadata_txt_content")]
        public string MetadataTxtContent { get; set; }
    }

    public class Scene
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("narration")]
        public string Narration { get; set; }

        [JsonProperty("telop")]
        public Telop? Telop { get; set; }

        [JsonProperty("telop_text")]
        public string? TelopText { get; set; }

        [JsonProperty("visual_prompt")]
        public string? VisualPrompt { get; set; }

        public string GetTelopText() => TelopText ?? Telop?.Text ?? "";

        [JsonProperty("visual")]
        public string Visual { get; set; }

        [JsonProperty("transition")]
        public string Transition { get; set; }

        [JsonProperty("voice")]
        public Voice Voice { get; set; }

        [JsonProperty("pause_after")]
        public double PauseAfter { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("music_sync")]
        public string MusicSync { get; set; }
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
