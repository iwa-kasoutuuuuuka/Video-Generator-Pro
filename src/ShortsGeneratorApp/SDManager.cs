using System;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using StableDiffusion.NET;
using HPPH;

namespace ShortsGeneratorApp
{
    public class SDManager : IDisposable
    {
        private DiffusionModel? _model;
        private string? _currentModelPath;

        public async Task InitializeAsync(string modelPath, string? vaePath = null)
        {
            if (_currentModelPath == modelPath && _model != null) return;

            await Task.Run(() => {
                _model?.Dispose();
                
                var parameter = DiffusionModelParameter.Create()
                    .WithModelPath(modelPath)
                    .WithVae(vaePath ?? "")
                    .WithMultithreading();

                _model = new DiffusionModel(parameter);
                _currentModelPath = modelPath;
            });
        }

        public async Task<byte[]> GenerateImageAsync(string prompt, string negativePrompt = "worst quality, low quality", int width = 512, int height = 512, int steps = 20, float cfgScale = 7.0f)
        {
            if (_model == null) throw new InvalidOperationException("SD Model not initialized");

            return await Task.Run(() => {
                var param = ImageGenerationParameter.TextToImage(prompt)
                    .WithNegativePrompt(negativePrompt)
                    .WithSize(width, height)
                    .WithSteps(steps)
                    .WithTxtCfg(cfgScale);

                var image = _model.GenerateImage(param);
                {
                    // Manual conversion to byte[] (PNG) via Bitmap
                    var pixels = image.ToArray();
                    int w = image.Width;
                    int h = image.Height;

                    using (var bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                    {
                        var rect = new Rectangle(0, 0, w, h);
                        var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

                        int bytesPerPixel = 3;
                        byte[] rowData = new byte[w * bytesPerPixel];

                        for (int y = 0; y < h; y++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                var color = pixels[y * w + x];
                                rowData[x * 3 + 0] = color.B;
                                rowData[x * 3 + 1] = color.G;
                                rowData[x * 3 + 2] = color.R;
                            }
                            IntPtr destRow = bmpData.Scan0 + (y * bmpData.Stride);
                            Marshal.Copy(rowData, 0, destRow, rowData.Length);
                        }

                        bitmap.UnlockBits(bmpData);

                        using (var ms = new MemoryStream())
                        {
                            bitmap.Save(ms, ImageFormat.Png);
                            return ms.ToArray();
                        }
                    }
                }
            });
        }

        public void Dispose()
        {
            _model?.Dispose();
        }
    }
}
