using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShortsGeneratorApp
{
    public class ModelManager
    {
        public string ModelsDir { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");

        public string FFMpegUrl { get; } = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        public string PiperUrl { get; } = "https://github.com/rhasspy/piper/releases/download/v1.2.0/piper_windows_amd64.zip";

        public List<AIModelInfo> LlmModels { get; } = new List<AIModelInfo>
        {
            new AIModelInfo { 
                Name = "Phi-3 Mini (高性能・軽量)", 
                Path = "Phi-3-mini-4k-instruct-q4.gguf", 
                DownloadUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf", 
                RequiredVram = "4GB" 
            },
            new AIModelInfo { 
                Name = "Llama-3 8B (高精度)", 
                Path = "Meta-Llama-3-8B-Instruct.Q4_K_M.gguf", 
                DownloadUrl = "https://huggingface.co/QuantFactory/Meta-Llama-3-8B-Instruct-GGUF/resolve/main/Meta-Llama-3-8B-Instruct.Q4_K_M.gguf", 
                RequiredVram = "8GB" 
            }
        };

        public List<AIModelInfo> SdModels { get; } = new List<AIModelInfo>
        {
            new AIModelInfo { 
                Name = "SD 1.5 (高速・汎用)", 
                Path = "v1-5-pruned-emaonly.safetensors", 
                DownloadUrl = "https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/v1-5-pruned-emaonly.safetensors", 
                RequiredVram = "4GB" 
            }
        };

        public List<AIModelInfo> VoiceModels { get; } = new List<AIModelInfo>
        {
            new AIModelInfo { 
                Name = "日本語 - 男性 (標準)", 
                Path = "ja_JP-natsuya-medium.onnx", 
                DownloadUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/ja/ja_JP/natsuya/medium/ja_JP-natsuya-medium.onnx", 
                ConfigUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/ja/ja_JP/natsuya/medium/ja_JP-natsuya-medium.onnx.json",
                RequiredVram = "0GB" 
            }
        };

        public ModelManager()
        {
            if (!Directory.Exists(ModelsDir))
            {
                Directory.CreateDirectory(ModelsDir);
            }
        }

        public async Task DownloadAndExtractPiperAsync(string url, Action<double> progressCallback)
        {
            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper.zip");
            await DownloadFileAsync(url, zipPath, progressCallback);

            await Task.Run(() =>
            {
                if (File.Exists(zipPath))
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, AppDomain.CurrentDomain.BaseDirectory, true);
                    File.Delete(zipPath);
                }
            });
        }

        public async Task DownloadFileAsync(string url, string destinationPath, Action<double> onProgress)
        {
            string tempPath = destinationPath + ".tmp";
            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var totalReadBytes = 0L;
                        var readBytes = 0;

                        while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, readBytes);
                            totalReadBytes += readBytes;

                            if (canReportProgress)
                            {
                                onProgress?.Invoke((double)totalReadBytes / totalBytes * 100);
                            }
                        }
                    }
                }
            }
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }

        public async Task DownloadAndExtractFFmpegAsync(string url, Action<double> onProgress)
        {
            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_temp.zip");
            string extractDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_extract");

            await DownloadFileAsync(url, zipPath, onProgress);

            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

            string[] files = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file).ToLower();
                if (fileName == "ffmpeg.exe" || fileName == "ffprobe.exe")
                {
                    string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(file, dest);
                }
            }

            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    public class AIModelInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ConfigUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string RequiredVram { get; set; } = "";
    }
}
