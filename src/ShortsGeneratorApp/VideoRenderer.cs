using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using FFMpegCore;

namespace ShortsGeneratorApp
{
    public class VideoRenderer
    {
        private SDManager? _sdManager;
        private Action<string>? _logger;

        // Shared semaphores to protect system resources globally
        private static readonly System.Threading.SemaphoreSlim _sdSemaphore = new System.Threading.SemaphoreSlim(1); // VRAM Protection
        private static readonly System.Threading.SemaphoreSlim _renderSemaphore = new System.Threading.SemaphoreSlim(4); // FFmpeg CPU/IO balance
        private static readonly System.Threading.SemaphoreSlim _speechSemaphore = new System.Threading.SemaphoreSlim(1); // Speech Engine Protection

        public void SetSDManager(SDManager sdManager) => _sdManager = sdManager;
        public void SetLogger(Action<string> logger) => _logger = logger;

        private void Log(string msg) => _logger?.Invoke($"[Renderer] {msg}");

        public async Task RenderVideoAsync(GenerationResult result, Variation variation, string outputPath)
        {
            string resolution = (result.Orientation?.Contains("Horizontal") == true) ? "1920x1080" : "1080x1920";
            string sizeArg = resolution.Replace("x", ":");
            int w = (resolution == "1920x1080") ? 1920 : 1080;
            int h = (resolution == "1920x1080") ? 1080 : 1920;

            string tempDir = Path.Combine(Path.GetTempPath(), "ShortsGen_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            Log($"Starting render for variation: {variation.VariationId} ({variation.Scenes.Count} scenes)");

            try
            {
                var clipTasks = new List<Task<string>>();

                for (int i = 0; i < variation.Scenes.Count; i++)
                {
                    int index = i;
                    var scene = variation.Scenes[i];
                    
                    clipTasks.Add(Task.Run(async () => {
                        string audioPath = Path.Combine(tempDir, $"scene_{index}.wav");
                        string imagePath = Path.Combine(tempDir, $"scene_{index}.png");
                        string clipPath = Path.Combine(tempDir, $"clip_{index}.mp4");

                        // 1. Parallel asset generation
                        var audioTask = Task.Run(async () => {
                            await SynthesizeSpeechAsync(scene.Narration, audioPath);
                        });

                        var imageTask = Task.Run(async () => {
                            if (_sdManager != null && !string.IsNullOrEmpty(scene.VisualPrompt))
                            {
                                await _sdSemaphore.WaitAsync();
                                try {
                                    byte[] imgData = await _sdManager.GenerateImageAsync(scene.VisualPrompt, width: w, height: h);
                                    await File.WriteAllBytesAsync(imagePath, imgData);
                                } catch (Exception ex) {
                                    Log($"SD Generation failed for scene {index}: {ex.Message}. Falling back to placeholder.");
                                    RenderFrame(scene, imagePath, w, h);
                                } finally {
                                    _sdSemaphore.Release();
                                }
                            }
                            else {
                                RenderFrame(scene, imagePath, w, h);
                            }
                        });

                        await Task.WhenAll(audioTask, imageTask);

                        // 2. Render Clip
                        await _renderSemaphore.WaitAsync();
                        try {
                            Log($"Rendering clip {index}/{variation.Scenes.Count}...");
                            await FFMpegArguments
                                .FromFileInput(imagePath, true, options => options.WithCustomArgument("-loop 1"))
                                .AddFileInput(audioPath)
                                .OutputToFile(clipPath, true, options => options
                                    .WithVideoCodec("libx264")
                                    .WithAudioCodec("aac")
                                    .WithCustomArgument($"-vf \"scale={sizeArg}:force_original_aspect_ratio=decrease,pad={sizeArg}:(ow-iw)/2:(oh-ih)/2,format=yuv420p\" -shortest"))
                                .ProcessAsynchronously();
                            return clipPath;
                        } finally {
                            _renderSemaphore.Release();
                        }
                    }));
                }

                var clips = await Task.WhenAll(clipTasks);

                Log("Concatenating all clips...");
                await Task.Run(() => FFMpeg.Join(outputPath, clips));
                Log($"Video rendered successfully: {outputPath}");
            }
            catch (Exception ex) {
                Log($"CRITICAL ERROR during render: {ex.Message}");
                throw;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private async Task SynthesizeSpeechAsync(string text, string outputPath)
        {
            string textToSpeak = string.IsNullOrWhiteSpace(text) ? " " : text;

            await _speechSemaphore.WaitAsync();
            try {
                await Task.Run(() => {
                    using (var synth = new SpeechSynthesizer())
                    {
                        synth.SetOutputToWaveFile(outputPath);
                        synth.Speak(textToSpeak);
                    }
                });
            } finally {
                _speechSemaphore.Release();
            }
        }

        private void RenderFrame(Scene scene, string outputPath, int width, int height)
        {
            using (var bitmap = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bitmap))
            {
                // Background
                g.Clear(Color.FromArgb(17, 25, 40));
                
                // Draw some abstract shapes for "visual"
                var brush = new LinearGradientBrush(new Rectangle(0,0,width,height), Color.FromArgb(139, 92, 246), Color.FromArgb(236, 72, 153), 45f);
                g.FillEllipse(brush, width * 0.1f, height * 0.2f, width * 0.8f, width * 0.8f);

                // Visual Prompt
                if (!string.IsNullOrEmpty(scene.VisualPrompt))
                {
                    var visualFont = new Font("Arial", 24, FontStyle.Italic);
                    g.DrawString($"[Scene Content: {scene.VisualPrompt}]", visualFont, Brushes.LightCyan, new RectangleF(20, 20, width - 40, 300));
                }

                // Telop
                string telopText = scene.GetTelopText();
                if (!string.IsNullOrEmpty(telopText))
                {
                    var font = new Font("Arial", 60, FontStyle.Bold);
                    var textSize = g.MeasureString(telopText, font);
                    float x = (width - textSize.Width) / 2;
                    float y = height * 0.85f;
                    
                    g.DrawString(telopText, font, Brushes.Black, x + 3, y + 3);
                    g.DrawString(telopText, font, Brushes.White, x, y);
                }

                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
