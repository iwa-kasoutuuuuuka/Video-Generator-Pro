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

        public void SetSDManager(SDManager sdManager) => _sdManager = sdManager;

        public async Task RenderVideoAsync(GenerationResult result, Variation variation, string outputPath)
        {
            string resolution = (result.Orientation?.Contains("Horizontal") == true) ? "1920x1080" : "1080x1920";
            string sizeArg = resolution.Replace("x", ":");
            int w = (resolution == "1920x1080") ? 1920 : 1080;
            int h = (resolution == "1920x1080") ? 1080 : 1920;

            string tempDir = Path.Combine(Path.GetTempPath(), "ShortsGen_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var sceneTasks = new List<Task>();
                string[] audioFiles = new string[variation.Scenes.Count];
                string[] imageFiles = new string[variation.Scenes.Count];
                
                // Semaphore to prevent OOM in VRAM for SD generation (max 1 at a time)
                var sdSemaphore = new System.Threading.SemaphoreSlim(1);

                for (int i = 0; i < variation.Scenes.Count; i++)
                {
                    int index = i;
                    var scene = variation.Scenes[i];
                    
                    sceneTasks.Add(Task.Run(async () => {
                        // 1. Generate Speech (Parallel)
                        string audioPath = Path.Combine(tempDir, $"scene_{index}.wav");
                        SynthesizeSpeech(scene.Narration, audioPath);
                        audioFiles[index] = audioPath;

                        // 2. Render Frame (SD is serialized via semaphore, but other parts are parallel)
                        string imagePath = Path.Combine(tempDir, $"scene_{index}.png");
                        if (_sdManager != null && !string.IsNullOrEmpty(scene.VisualPrompt))
                        {
                            await sdSemaphore.WaitAsync();
                            try {
                                byte[] imgData = await _sdManager.GenerateImageAsync(scene.VisualPrompt, width: w, height: h);
                                File.WriteAllBytes(imagePath, imgData);
                            } catch {
                                RenderFrame(scene, imagePath, w, h);
                            } finally {
                                sdSemaphore.Release();
                            }
                        }
                        else {
                            RenderFrame(scene, imagePath, w, h);
                        }
                        imageFiles[index] = imagePath;
                    }));
                }

                await Task.WhenAll(sceneTasks);

                // 3. Render Clips in Parallel
                var clipTasks = new List<Task<string>>();
                var renderSemaphore = new System.Threading.SemaphoreSlim(4); // Max 4 parallel FFmpeg jobs

                for (int i = 0; i < audioFiles.Length; i++)
                {
                    int index = i;
                    clipTasks.Add(Task.Run(async () => {
                        await renderSemaphore.WaitAsync();
                        try {
                            string clipPath = Path.Combine(tempDir, $"clip_{index}.mp4");
                            await FFMpegArguments
                                .FromFileInput(imageFiles[index], true, options => options.WithCustomArgument("-loop 1"))
                                .AddFileInput(audioFiles[index])
                                .OutputToFile(clipPath, true, options => options
                                    .WithVideoCodec("libx264")
                                    .WithAudioCodec("aac")
                                    .WithCustomArgument($"-vf \"scale={sizeArg}:force_original_aspect_ratio=decrease,pad={sizeArg}:(ow-iw)/2:(oh-ih)/2,format=yuv420p\" -shortest"))
                                .ProcessAsynchronously();
                            return clipPath;
                        } finally {
                            renderSemaphore.Release();
                        }
                    }));
                }

                var clips = await Task.WhenAll(clipTasks);

                // 4. Concatenate all clips
                await Task.Run(() => FFMpeg.Join(outputPath, clips));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static readonly System.Threading.SemaphoreSlim _speechSemaphore = new System.Threading.SemaphoreSlim(1);

        private void SynthesizeSpeech(string text, string outputPath)
        {
            // Fix: 'textToSpeak' cannot be null or empty for SpeechSynthesizer
            string textToSpeak = string.IsNullOrWhiteSpace(text) ? " " : text;

            _speechSemaphore.Wait();
            try {
                using (var synth = new SpeechSynthesizer())
                {
                    synth.SetOutputToWaveFile(outputPath);
                    synth.Speak(textToSpeak);
                }
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

                // Visual Prompt (Suggestion for the scene)
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
                    
                    // Shadow
                    g.DrawString(telopText, font, Brushes.Black, x + 3, y + 3);
                    // Main Text
                    g.DrawString(telopText, font, Brushes.White, x, y);
                }

                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
