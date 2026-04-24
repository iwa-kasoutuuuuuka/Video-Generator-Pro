using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;
using FFMpegCore;
using FFMpegCore.Enums;

namespace ShortsGeneratorApp
{
    public class VideoRenderer
    {
        private SDManager? _sdManager;
        private Action<string>? _logger;
        private string _voiceModelPath = "";

        private static readonly System.Threading.SemaphoreSlim _sdSemaphore = new System.Threading.SemaphoreSlim(1);
        private static readonly System.Threading.SemaphoreSlim _renderSemaphore = new System.Threading.SemaphoreSlim(4);
        private static readonly System.Threading.SemaphoreSlim _speechSemaphore = new System.Threading.SemaphoreSlim(1);

        public void SetSDManager(SDManager sdManager) => _sdManager = sdManager;
        public void SetLogger(Action<string> logger) => _logger = logger;
        public void SetVoiceModel(string path) => _voiceModelPath = path;

        private void Log(string msg) => _logger?.Invoke($"[Renderer] {msg}");

        public async Task RenderVideoV2Async(V2GenerationResult result, string outputPath, string bgmPath = "")
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ShortsGenV2_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            Log($"Starting v2.2 Pro Render... ({result.Scenes.Count} scenes)");

            try
            {
                var clipTasks = new List<Task<string>>();
                int w = 1080;
                int h = 1920;

                // Detect GPU for hardware acceleration
                string videoCodec = DetectBestCodec();
                Log($"Using Video Codec: {videoCodec}");

                for (int i = 0; i < result.Scenes.Count; i++)
                {
                    int index = i;
                    var scene = result.Scenes[i];
                    
                    clipTasks.Add(Task.Run(async () => {
                        string audioPath = Path.Combine(tempDir, $"scene_{index}.wav");
                        string imagePath = Path.Combine(tempDir, $"scene_{index}.png");
                        string clipPath = Path.Combine(tempDir, $"clip_{index}.mp4");

                        // 1. Parallel asset generation with retries
                        var audioTask = SynthesizeSpeechPiperAsync(scene.Narration, audioPath);
                        var imageTask = GenerateImageWithRetryAsync(scene, imagePath, w, h, 2);

                        await Task.WhenAll(audioTask, imageTask);

                        // 2. Render Clip with dynamic zoompan + audio fade
                        await _renderSemaphore.WaitAsync();
                        try {
                            await FFMpegArguments
                                .FromFileInput(imagePath, true, options => options.WithCustomArgument("-loop 1"))
                                .AddFileInput(audioPath)
                                .OutputToFile(clipPath, true, options => options
                                    .WithVideoCodec(videoCodec)
                                    .WithAudioCodec("aac")
                                    .WithCustomArgument(videoCodec.Contains("nvenc") ? "-preset p4" : "-preset veryfast")
                                    // AF: afade to prevent pops, VF: zoompan
                                    .WithCustomArgument($"-af \"afade=t=in:ss=0:d=0.1,afade=t=out:st=3:d=0.1\"") // st will be adjusted by -shortest
                                    .WithCustomArgument($"-vf \"scale=2000:-1,zoompan=z='min(zoom+0.0015,1.3)':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s={w}x{h},format=yuv420p\" -shortest"))
                                .ProcessAsynchronously();
                            return clipPath;
                        } finally {
                            _renderSemaphore.Release();
                        }
                    }));
                }

                var clips = await Task.WhenAll(clipTasks);

                // Create Concat List
                string listPath = Path.Combine(tempDir, "list.txt");
                File.WriteAllLines(listPath, Array.ConvertAll(clips, c => $"file '{c.Replace("\\", "/")}'"));

                // Generate Advanced ASS Subtitles
                string assPath = Path.Combine(tempDir, "subtitles.ass");
                GenerateAssFile(result.Srt, assPath);
                string escapedAssPath = assPath.Replace("\\", "/").Replace(":", "\\:");

                Log("Applying Final Effects: Sidechain Ducking + ASS Subtitles...");

                // Final Assembly with Sidechain Auto-Ducking and Faststart
                var ffmpeg = FFMpegArguments.FromFileInput(listPath, false, options => options.WithCustomArgument("-f concat -safe 0"));
                
                string audioFilter = "[0:a]aresample=44100,dynaudnorm=p=0.9:m=100[aout]";
                if (File.Exists(bgmPath))
                {
                    ffmpeg.AddFileInput(bgmPath, true, options => options.WithCustomArgument("-stream_loop -1"));
                    audioFilter = "[1:a]volume=0.2[bg];[0:a][bg]amix=inputs=2:duration=first[mixed];[mixed]aresample=44100,dynaudnorm=p=0.9:m=100[aout]";
                }

                await ffmpeg.OutputToFile(outputPath, true, options => options
                        .WithVideoCodec(videoCodec)
                        .WithAudioCodec("aac")
                        .WithCustomArgument($"-filter_complex \"{audioFilter}\" -map 0:v -map [aout] -vf \"ass='{escapedAssPath}'\" -movflags +faststart")
                        .WithCustomArgument(videoCodec.Contains("nvenc") ? "-preset p4" : "-preset fast"))
                    .ProcessAsynchronously();

                Log($"v2.5 Video rendered successfully: {outputPath}");
            }
            catch (Exception ex) {
                Log($"CRITICAL ERROR: {ex.Message}");
                throw;
            }
            finally {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private async Task GenerateImageWithRetryAsync(V2Scene scene, string imagePath, int w, int h, int maxRetries)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try {
                    await GenerateImageOrPlaceholderAsync(scene, imagePath, w, h);
                    if (File.Exists(imagePath) && new FileInfo(imagePath).Length > 1000) return;
                } catch {
                    if (i == maxRetries - 1) throw;
                }
            }
        }

        private void GenerateAssFile(string srtContent, string outputPath)
        {
            // Simplified ASS generation: converts SRT blocks to ASS Dialogue
            var header = @"[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,MS UI Gothic,85,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,6,3,2,10,10,400,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
";
            // Use UTF-8 without BOM for ASS files
            using (var sw = new StreamWriter(outputPath, false, new System.Text.UTF8Encoding(false)))
            {
                sw.Write(header);
                // Basic SRT to ASS conversion logic
                var lines = srtContent.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in lines)
                {
                    var parts = block.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var timeMatch = System.Text.RegularExpressions.Regex.Match(parts[1], @"(\d{2}:\d{2}:\d{2},\d{3}) --> (\d{2}:\d{2}:\d{2},\d{3})");
                        if (timeMatch.Success)
                        {
                            string start = timeMatch.Groups[1].Value.Replace(',', '.').Substring(1); // 00:00:00.00
                            string end = timeMatch.Groups[2].Value.Replace(',', '.').Substring(1);
                            string text = string.Join("\\N", parts.Skip(2)).Trim();
                            sw.WriteLine($"Dialogue: 0,{start},{end},Default,,0,0,0,,{text}");
                        }
                    }
                }
            }
        }

        private string DetectBestCodec()
        {
            try {
                // Run a tiny test command to check for NVENC
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = "-f lavfi -i color=c=black:s=64x64:d=0.1 -c:v h264_nvenc -f null -",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0) return "h264_nvenc";
                }
            } catch { }
            return "libx264"; 
        }

        private async Task SynthesizeSpeechPiperAsync(string text, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(text)) {
                await CreateSilentWavAsync(outputPath, 1.0); // Create 1s silent wav
                return;
            }
            
            await _speechSemaphore.WaitAsync();
            try {
                string piperExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper.exe");
                if (!File.Exists(piperExe)) {
                    Log("Piper.exe not found! Falling back to System.Speech.");
                    await SynthesizeSpeechLegacyAsync(text, outputPath);
                    return;
                }

                if (!File.Exists(_voiceModelPath)) {
                    Log("Voice model not found! Falling back to System.Speech.");
                    await SynthesizeSpeechLegacyAsync(text, outputPath);
                    return;
                }

                await Task.Run(() => {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = piperExe,
                        Arguments = $"--model \"{_voiceModelPath}\" --output_file \"{outputPath}\"",
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null) {
                            try {
                                process.StandardInput.Write(text);
                                process.StandardInput.Close(); 
                                if (!process.WaitForExit(60000)) { // 60s timeout
                                    process.Kill();
                                    Log("Piper timeout (60s)");
                                }
                            } catch (Exception ex) {
                                Log($"Piper stream error: {ex.Message}");
                            }
                        }
                    }
                });

                // Final check to ensure file exists
                if (!File.Exists(outputPath)) {
                    Log("Piper failed to create file. Creating silent fallback.");
                    await CreateSilentWavAsync(outputPath, 1.0);
                }
            } catch (Exception ex) {
                Log($"Synthesize Error: {ex.Message}");
                await CreateSilentWavAsync(outputPath, 1.0);
            } finally {
                _speechSemaphore.Release();
            }
        }

        private async Task SynthesizeSpeechLegacyAsync(string text, string outputPath)
        {
            await Task.Run(() => {
                using (var synth = new System.Speech.Synthesis.SpeechSynthesizer()) {
                    synth.SetOutputToWaveFile(outputPath);
                    synth.Speak(text);
                }
            });
        }

        private async Task GenerateImageOrPlaceholderAsync(V2Scene scene, string imagePath, int w, int h)
        {
            if (_sdManager != null && !string.IsNullOrEmpty(scene.VisualPrompt))
            {
                await _sdSemaphore.WaitAsync();
                try {
                    byte[] imgData = await _sdManager.GenerateImageAsync(scene.VisualPrompt, width: w, height: h);
                    await File.WriteAllBytesAsync(imagePath, imgData);
                } catch {
                    RenderPlaceholder(scene.Narration, imagePath, w, h);
                } finally {
                    _sdSemaphore.Release();
                }
            }
            else {
                RenderPlaceholder(scene.Narration, imagePath, w, h);
            }
        }

        public async Task RenderThumbnailAsync(V2GenerationResult result, string bgImagePath, string outputPath)
        {
            int w = 1280;
            int h = 720;
            using (var bitmap = File.Exists(bgImagePath) ? new Bitmap(bgImagePath) : new Bitmap(w, h))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                string t1 = result.ThumbnailTexts.Count > 0 ? result.ThumbnailTexts[0] : "";
                string t2 = result.ThumbnailTexts.Count > 1 ? result.ThumbnailTexts[1] : "";
                DrawThumbnailText(g, t1, w, h * 0.2f, 80, Color.White, Color.Black);
                DrawThumbnailText(g, t2, w, h * 0.5f, 60, Color.Yellow, Color.Black);
                bitmap.Save(outputPath, ImageFormat.Jpeg);
            }
        }

        private void DrawThumbnailText(Graphics g, string text, int w, float y, int fontSize, Color textColor, Color boxColor)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var font = new Font("MS UI Gothic", fontSize, FontStyle.Bold))
            {
                var size = g.MeasureString(text, font);
                float x = (w - size.Width) / 2;
                using (var brush = new SolidBrush(Color.FromArgb(200, boxColor))) {
                    g.FillRectangle(brush, x - 20, y - 10, size.Width + 40, size.Height + 20);
                }
                g.DrawString(text, font, Brushes.Black, x + 3, y + 3);
                g.DrawString(text, font, new SolidBrush(textColor), x, y);
            }
        }

        private void RenderPlaceholder(string text, string outputPath, int w, int h)
        {
            using (var bitmap = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bitmap)) {
                g.Clear(Color.FromArgb(17, 25, 40));
                g.DrawString(text, new Font("Arial", 24), Brushes.Gray, new RectangleF(40, 40, w - 80, h - 80));
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }
        private async Task CreateSilentWavAsync(string outputPath, double durationSeconds)
        {
            try {
                // Use a direct process call for maximum reliability in creating the silent fallback
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-f lavfi -i anullsrc=r=22050:cl=mono -t {durationSeconds} -y \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(startInfo))
                {
                    if (process != null) await process.WaitForExitAsync();
                }
            } catch (Exception ex) {
                Log($"Critical failure in silent wav fallback: {ex.Message}");
            }
        }
    }
}
