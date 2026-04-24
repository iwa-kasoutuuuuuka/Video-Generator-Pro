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

        // --- 確実に存在が確認されたURL ---
        public string FFMpegUrl { get; } = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        public string PiperUrl { get; } = "https://github.com/rhasspy/piper/releases/download/v1.2.0/piper_windows_amd64.zip";

        public List<AIModelInfo> LlmModels { get; } = new List<AIModelInfo>
        {
            // ✅ 実在確認済み: Phi-3-mini-4k-instruct-q4.gguf (2.39 GB)
            new AIModelInfo { 
                Name = "Phi-3 Mini (高性能・軽量)", 
                Path = "Phi-3-mini-4k-instruct-q4.gguf", 
                DownloadUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf", 
                RequiredVram = "4GB" 
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
            // ✅ 実在確認済み: en_US-lessac-medium (63.2 MB)
            // 日本語モデルはPiper公式に存在しないため、英語をデフォルトとする
            new AIModelInfo { 
                Name = "English - Lessac (高品質)", 
                Path = "en_US-lessac-medium.onnx", 
                DownloadUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx", 
                ConfigUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json",
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
                    // piper_windows_amd64.zip は piper/ サブフォルダに展開される
                    string extractTarget = AppDomain.CurrentDomain.BaseDirectory;
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractTarget, true);
                    
                    // piper/piper.exe を BaseDirectory にコピー
                    string piperInSubDir = Path.Combine(extractTarget, "piper", "piper.exe");
                    string piperDest = Path.Combine(extractTarget, "piper.exe");
                    if (File.Exists(piperInSubDir) && !File.Exists(piperDest))
                    {
                        File.Copy(piperInSubDir, piperDest);
                    }
                    
                    File.Delete(zipPath);
                }
            });
        }

        public async Task DownloadFileAsync(string url, string destinationPath, Action<double> onProgress)
        {
            string tempPath = destinationPath + ".tmp";
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30); // 大容量ファイル対応
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
                // ダウンロード完了時のみリネーム（アトミック）
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                File.Move(tempPath, destinationPath);
            }
            catch
            {
                // 失敗時は一時ファイルを削除
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
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
