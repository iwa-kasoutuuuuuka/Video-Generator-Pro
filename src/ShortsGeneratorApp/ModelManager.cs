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

        public List<AIModelInfo> LlmModels { get; } = new List<AIModelInfo>
        {
            new AIModelInfo { 
                Name = "Phi-3 Mini (3.8B)", 
                Path = "phi-3-mini.gguf", 
                DownloadUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf",
                Description = "超軽量・高速 (Microsoft)", 
                RequiredVram = "2.5GB" 
            },
            new AIModelInfo { 
                Name = "Gemma-2-2B", 
                Path = "gemma-2-2b.gguf", 
                DownloadUrl = "https://huggingface.co/bartowski/gemma-2-2b-it-GGUF/resolve/main/gemma-2-2b-it-Q4_K_M.gguf",
                Description = "極小モデル・省電力 (Google)", 
                RequiredVram = "1.8GB" 
            }
        };

        public string FFMpegUrl { get; } = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        public List<AIModelInfo> SdModels { get; } = new List<AIModelInfo>
        {
            new AIModelInfo { Name = "SD v1.5 Pruned", Path = "v1-5-pruned.safetensors", Description = "軽量・標準的ビジュアル", RequiredVram = "3GB" },
            new AIModelInfo { Name = "SD Lightning (Fast)", Path = "sd-lightning.safetensors", Description = "超高速 (1-4ステップ)", RequiredVram = "4GB" }
        };

        public ModelManager()
        {
            if (!Directory.Exists(ModelsDir))
            {
                Directory.CreateDirectory(ModelsDir);
            }
        }

        public async Task DownloadFileAsync(string url, string destinationPath, Action<double> onProgress)
        {
            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
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
        }

        public async Task DownloadAndExtractFFmpegAsync(string url, Action<double> onProgress)
        {
            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_temp.zip");
            string extractDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_extract");

            await DownloadFileAsync(url, zipPath, onProgress);

            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Find ffmpeg.exe and ffprobe.exe recursively in extracted folder
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

            // Cleanup
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    public class AIModelInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string RequiredVram { get; set; } = "";
    }
}
