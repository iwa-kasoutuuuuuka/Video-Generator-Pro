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

        // プロンプトのテンプレート定義（verbatim文字列のエスケープ問題を回避するため定数化）
        private const string PromptTemplate =
            "[ROLE]\n" +
            "あなたは\"YouTube/Shorts動画の視聴維持率・CTR最適化AI\"です。\n" +
            "視聴者が最後まで飽きずに視聴し、クリックしたくなる台本とメタデータを生成してください。\n\n" +
            "[TASK]\n" +
            "入力されたコンテンツを分析し、YouTubeアルゴリズム（満足度・維持率）に最適化された出力を生成せよ。\n\n" +
            "[RULES]\n" +
            "- 導入は最初の5秒で視聴者を引き込む強力なフックを設計すること。\n" +
            "- 内容は中学生でも理解できる平易かつ、感情を動かすテンポの良い表現にする。\n" +
            "- 出力は必ず下記のJSON構造に従い、純粋なJSONのみを返せ。\n" +
            "- 日本語を使用すること。ただしvisual_promptはStable Diffusion向けに英語で記述せよ。\n" +
            "- 解説・前置き・Markdown記号（```や###）は一切禁止する。\n\n" +
            "[SUCCESS EXAMPLE]\n" +
            "{\n" +
            "  \"titles\": [\"驚愕の真実！\", \"誰も知らない裏技\", \"3分でわかる解説\"],\n" +
            "  \"thumbnail_texts\": [\"ヤバイ\", \"禁断の手法\"],\n" +
            "  \"tags\": [\"解説\", \"豆知識\"],\n" +
            "  \"description\": \"今回の動画では...\",\n" +
            "  \"hook\": \"ねぇ、これ知ってた？\",\n" +
            "  \"script_full\": \"こんにちは！今日は...\",\n" +
            "  \"scenes\": [\n" +
            "    {\n" +
            "      \"id\": 1,\n" +
            "      \"narration\": \"こんにちは！今日は驚きの事実をお伝えします。\",\n" +
            "      \"visual_prompt\": \"A surprised young woman pointing at a glowing smartphone, cinematic 8k\",\n" +
            "      \"duration_seconds\": 5\n" +
            "    }\n" +
            "  ],\n" +
            "  \"srt\": \"1\\n00:00:00,000 --> 00:00:05,000\\nこんにちは！\",\n" +
            "  \"risk_check\": \"なし\"\n" +
            "}\n\n" +
            "[STRICT RULE]\n" +
            "あなたはJSON生成機械です。{ で始まり } で終わるデータ以外を1文字でも出力したらエラーとなります。\n\n" +
            "[INPUT]\n" +
            "__INPUT_DATA__";

        public async Task<V2GenerationResult> GenerateV2Async(string inputUrlOrText, int duration, string style)
        {
            // プロンプト生成：PromptTemplateの__INPUT_DATA__にユーザー入力を差し込む（エスケープ安全）
            string prompt = PromptTemplate.Replace("__INPUT_DATA__", inputUrlOrText);

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

            // 1. Markdownコードブロック内のJSONを優先的に検出
            var mdMatch = Regex.Match(input, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.Singleline);
            if (mdMatch.Success)
            {
                var candidate = mdMatch.Groups[1].Value.Trim();
                if (candidate.StartsWith("{")) return candidate;
            }

            // 2. ブラケットカウント法：最初の'{' からネストを追跡して完全なJSONブロックを抽出
            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start != -1)
                    {
                        return input.Substring(start, i - start + 1).Trim();
                    }
                }
            }

            return ""; // JSONが見つからない
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
