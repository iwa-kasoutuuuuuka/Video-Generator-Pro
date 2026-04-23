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
                var audioFiles = new List<string>();
                var imageFiles = new List<string>();

                for (int i = 0; i < variation.Scenes.Count; i++)
                {
                    var scene = variation.Scenes[i];
                    
                    // 1. Generate Speech
                    string audioPath = Path.Combine(tempDir, $"scene_{i}.wav");
                    SynthesizeSpeech(scene.Narration, audioPath);
                    audioFiles.Add(audioPath);

                    // 2. Render Frame
                    string imagePath = Path.Combine(tempDir, $"scene_{i}.png");
                    
                    if (_sdManager != null && !string.IsNullOrEmpty(scene.VisualPrompt))
                    {
                        try {
                            byte[] imgData = await _sdManager.GenerateImageAsync(scene.VisualPrompt, width: w, height: h);
                            File.WriteAllBytes(imagePath, imgData);
                        } catch {
                            RenderFrame(scene, imagePath, w, h); // Fallback to shapes
                        }
                    }
                    else {
                        RenderFrame(scene, imagePath, w, h);
                    }
                    
                    imageFiles.Add(imagePath);
                }

                var clips = new List<string>();
                for (int i = 0; i < audioFiles.Count; i++)
                {
                    string clipPath = Path.Combine(tempDir, $"clip_{i}.mp4");
                    
                    // Get audio duration
                    var audioAnalysis = FFProbe.Analyse(audioFiles[i]);
                    
                    // Create clip: Image + Audio
                    await FFMpegArguments
                        .FromFileInput(imageFiles[i], true, options => options.WithCustomArgument("-loop 1"))
                        .AddFileInput(audioFiles[i])
                        .OutputToFile(clipPath, true, options => options
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithCustomArgument($"-vf \"scale={sizeArg}:force_original_aspect_ratio=decrease,pad={sizeArg}:(ow-iw)/2:(oh-ih)/2,format=yuv420p\" -shortest"))
                        .ProcessAsynchronously();
                    
                    clips.Add(clipPath);
                }

                // Concatenate all clips
                await Task.Run(() => FFMpeg.Join(outputPath, clips.ToArray()));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private void SynthesizeSpeech(string text, string outputPath)
        {
            // Fix: 'textToSpeak' cannot be null or empty for SpeechSynthesizer
            string textToSpeak = string.IsNullOrWhiteSpace(text) ? " " : text;

            using (var synth = new SpeechSynthesizer())
            {
                synth.SetOutputToWaveFile(outputPath);
                synth.Speak(textToSpeak);
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
