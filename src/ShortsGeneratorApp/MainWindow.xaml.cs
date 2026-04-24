using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using FFMpegCore;

namespace ShortsGeneratorApp
{
    public partial class MainWindow : Window
    {
        private ModelManager _modelManager = new ModelManager();
        private LocalGeneratorEngine _localAI;
        private GeneratorEngine _realEngine;
        private VideoRenderer _renderer = new VideoRenderer();
        private SDManager _sdManager = new SDManager();
        private AppSettings _settings = new AppSettings();
        private bool _isProcessing = false;
        private string _settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private string _logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_log.txt");

        private static readonly object _logLock = new object();
        private void Log(string msg)
        {
            lock (_logLock)
            {
                try
                {
                    System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                }
                catch { }
            }
        }
        
        public MainWindow()
        {
            try {
                InitializeComponent();
                LoadSettings();
                InitializeSettings();
                _localAI = new LocalGeneratorEngine();
                _realEngine = new GeneratorEngine(_localAI);
                
                // Configure FFMpeg path
                string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (System.IO.File.Exists(ffmpegPath)) {
                    GlobalFFOptions.Configure(options => options.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory);
                }

                _renderer.SetLogger(Log);
                this.Title = "Shorts Video Generator PRO v2.6 [Premium Edition]";
                
                // Startup check for dependencies
                Dispatcher.BeginInvoke(new Action(async () => {
                    try {
                        await CheckAndDownloadModelsAsync();
                        await CheckAndDownloadFFmpegAsync();
                        await CheckAndDownloadPiperAsync();
                        await UpdateSystemStatusAsync();
                    } catch (Exception ex) {
                        MessageBox.Show($"セットアップ中にエラーが発生しました。インターネット接続を確認してください。\n詳細: {ex.Message}", "セットアップエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        Log($"Startup Error: {ex}");
                    }
                }));
            }
            catch (Exception ex) {
                MessageBox.Show($"Startup Error: {ex.Message}\n{ex.StackTrace}", "Critical Error");
            }
        }

        private async Task CheckAndDownloadModelsAsync()
        {
            var defaultModel = _modelManager.LlmModels[0];
            string modelPath = System.IO.Path.Combine(_modelManager.ModelsDir, defaultModel.Path);

            if (!System.IO.File.Exists(modelPath))
            {
                var result = MessageBox.Show(
                    $"標準AIモデル ({defaultModel.Name}) が見つかりません。\n自動ダウンロードを開始しますか？\n(約2.3GB、インターネット接続が必要です)", 
                    "セットアップ", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingProgressBar.IsIndeterminate = false;
                    
                    try {
                        await _modelManager.DownloadFileAsync(defaultModel.DownloadUrl, modelPath, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"モデルをダウンロード中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                        MessageBox.Show("ダウンロードが完了しました！", "完了");
                    }
                    catch (Exception ex) {
                        MessageBox.Show($"ダウンロードに失敗しました: {ex.Message}", "エラー");
                    }
                    finally {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        LoadingProgressBar.IsIndeterminate = true;
                    }
                }
            }

            // Also check for default voice model
            var defaultVoice = _modelManager.VoiceModels[0];
            string voicePath = Path.Combine(_modelManager.ModelsDir, defaultVoice.Path);
            if (!File.Exists(voicePath))
            {
                var result = MessageBox.Show($"音声モデル ({defaultVoice.Name}) が見つかりません。\nダウンロードしますか？ (約63MB)", "セットアップ", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    try {
                        // Download model
                        await _modelManager.DownloadFileAsync(defaultVoice.DownloadUrl, voicePath, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"音声モデルをダウンロード中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                        // Download config
                        if (!string.IsNullOrEmpty(defaultVoice.ConfigUrl))
                        {
                            string configPath = voicePath + ".json";
                            await _modelManager.DownloadFileAsync(defaultVoice.ConfigUrl, configPath, _ => { });
                        }
                    } finally {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private async Task CheckAndDownloadPiperAsync()
        {
            string piperExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper.exe");
            if (!File.Exists(piperExe))
            {
                var result = MessageBox.Show("高音質ナレーションエンジン (Piper) が見つかりません。セットアップしますか？", "セットアップ", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    try {
                        await _modelManager.DownloadAndExtractPiperAsync(_modelManager.PiperUrl, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"Piperをセットアップ中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                    } catch (Exception ex) {
                        MessageBox.Show($"Piperのセットアップに失敗しました: {ex.Message}");
                    } finally {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private async Task UpdateSystemStatusAsync()
        {
            try {
                // Check GPU (NVENC)
                string codec = await Task.Run(() => DetectBestCodecInternal());
                Dispatcher.Invoke(() => {
                    if (codec.Contains("nvenc"))
                    {
                        GpuStatusLight.Fill = System.Windows.Media.Brushes.LimeGreen;
                        GpuStatusText.Text = "GPU: NVENC (ON)";
                    }
                    else
                    {
                        GpuStatusLight.Fill = System.Windows.Media.Brushes.Gray;
                        GpuStatusText.Text = "GPU: CPU (libx264)";
                    }

                    // Check Piper
                    if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper.exe")))
                    {
                        PiperStatusLight.Fill = System.Windows.Media.Brushes.Cyan;
                        PiperStatusText.Text = "Piper: READY";
                    }
                    else
                    {
                        PiperStatusLight.Fill = System.Windows.Media.Brushes.Gray;
                        PiperStatusText.Text = "Piper: NOT FOUND";
                    }
                });
            } catch (Exception ex) {
                Log($"Status Diagnostic Error: {ex.Message}");
            }
        }

        private string DetectBestCodecInternal()
        {
            try {
                string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (!File.Exists(ffmpegPath)) return "libx264";

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-f lavfi -i color=c=black:s=64x64:d=0.1 -c:v h264_nvenc -f null -",
                    UseShellExecute = false, CreateNoWindow = true
                };
                using (var process = Process.Start(startInfo)) {
                    process.WaitForExit();
                    if (process.ExitCode == 0) return "h264_nvenc";
                }
            } catch { }
            return "libx264";
        }

        private async Task CheckAndDownloadFFmpegAsync()
        {
            string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            string ffprobePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");

            if (!System.IO.File.Exists(ffmpegPath) || !System.IO.File.Exists(ffprobePath))
            {
                var result = MessageBox.Show(
                    "動画書き出しに必要なコンポーネント (FFmpeg) が見つかりません。自動的にセットアップしますか？\n(約100MB)", 
                    "セットアップ", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingProgressBar.IsIndeterminate = false;
                    try {
                        await _modelManager.DownloadAndExtractFFmpegAsync(_modelManager.FFMpegUrl, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"FFmpegをダウンロード・展開中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                        MessageBox.Show("FFmpegのセットアップが完了しました！", "完了");
                    }
                    catch (Exception ex) {
                        MessageBox.Show($"FFmpegのセットアップに失敗しました: {ex.Message}", "エラー");
                    }
                    finally {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        LoadingProgressBar.IsIndeterminate = true;
                    }
                }
            }
            else {
                GlobalFFOptions.Configure(options => options.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        private void LoadSettings()
        {
            if (System.IO.File.Exists(_settingsPath))
            {
                try {
                    string json = System.IO.File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                } catch { }
            }
            
            if (string.IsNullOrEmpty(_settings.OutputPath)) {
                _settings.OutputPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            }
            OutputPathTextBlock.Text = _settings.OutputPath;
        }

        private void SaveSettings()
        {
            try {
                _settings.OutputPath = OutputPathTextBlock.Text;
                string json = JsonConvert.SerializeObject(_settings);
                System.IO.File.WriteAllText(_settingsPath, json);
            } catch { }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = OutputPathTextBlock.Text,
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath)) {
                    OutputPathTextBlock.Text = folderPath;
                    SaveSettings();
                }
            }
        }

        private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DurationValueText != null) {
                DurationValueText.Text = $"{(int)e.NewValue}s";
            }
        }

        private void TargetCharsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CharsValueText != null) {
                CharsValueText.Text = $"{(int)e.NewValue}";
            }
        }

        private void InitializeSettings()
        {
            LlmModelCombo.ItemsSource = _modelManager.LlmModels.Select(m => $"{m.Name} ({m.RequiredVram})");
            LlmModelCombo.SelectedIndex = 0;

            SdModelCombo.ItemsSource = _modelManager.SdModels.Select(m => $"{m.Name} ({m.RequiredVram})");
            SdModelCombo.SelectedIndex = 0;

            VoiceModelCombo.ItemsSource = _modelManager.VoiceModels.Select(m => m.Name);
            VoiceModelCombo.SelectedIndex = 0;

            LoadBgmList();
        }

        private void LoadBgmList()
        {
            string bgmDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BGM");
            if (!Directory.Exists(bgmDir)) Directory.CreateDirectory(bgmDir);

            var files = Directory.GetFiles(bgmDir, "*.*")
                .Where(s => s.EndsWith(".mp3") || s.EndsWith(".wav"))
                .ToList();

            BgmCombo.ItemsSource = files.Select(Path.GetFileName);
            if (files.Count > 0) BgmCombo.SelectedIndex = 0;
        }

        private void BgmBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav" };
            if (dialog.ShowDialog() == true)
            {
                var list = (BgmCombo.ItemsSource as IEnumerable<string>)?.ToList() ?? new List<string>();
                string fileName = Path.GetFileName(dialog.FileName);
                // Copy to BGM folder for persistence
                string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BGM", fileName);
                try { File.Copy(dialog.FileName, dest, true); } catch { }
                LoadBgmList();
                BgmCombo.SelectedItem = fileName;
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            string blogText = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(blogText))
            {
                MessageBox.Show("テキストを入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isProcessing = true;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "AIがコンテンツ戦略を立案中...";
            GenerateButton.IsEnabled = false;
            ResultPanel.Children.Clear();

            try
            {
                // 1. Check/Load LLM Model
                int llmIndex = LlmModelCombo.SelectedIndex;
                var llmInfo = _modelManager.LlmModels[llmIndex];
                string llmPath = System.IO.Path.Combine(_modelManager.ModelsDir, llmInfo.Path);

                if (!System.IO.File.Exists(llmPath))
                {
                    var dlResult = MessageBox.Show($"モデル ({llmInfo.Name}) が見つかりません。ダウンロードしますか？", "モデル不足", MessageBoxButton.YesNo);
                    if (dlResult == MessageBoxResult.Yes)
                    {
                        await _modelManager.DownloadFileAsync(llmInfo.DownloadUrl, llmPath, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"ダウンロード中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                    }
                    else return;
                }

                int gpuLayers = (UseGpuCheck.IsChecked == true) ? 20 : 0;
                await _localAI.InitializeAsync(llmPath, gpuLayers);

                // 2. Generate Script (v2.0)
                int duration = (int)DurationSlider.Value;
                string style = (StyleCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "標準";

                var result = await _realEngine.GenerateV2Async(blogText, duration, style);
                
                Log("v2.0 Generation successful.");
                DisplayV2Results(result);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show($"エラーが発生しました: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                GenerateButton.IsEnabled = true;
            }
        }

        private void DisplayV2Results(V2GenerationResult result)
        {
            ResultPanel.Children.Clear();

            // --- Strategy Panel ---
            var strategyPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            strategyPanel.Children.Add(new TextBlock { Text = "🚀 AIによる分析結果 (CTR・維持率最適化)", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.Gold, Margin = new Thickness(0,0,0,10) });
            
            // Titles
            strategyPanel.Children.Add(new TextBlock { Text = "【高CTRタイトル案】", FontWeight = FontWeights.Bold, Margin = new Thickness(0,5,0,5) });
            foreach(var t in result.Titles) {
                var tb = new TextBox { Text = t, IsReadOnly = true, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0,0,0,1), Margin = new Thickness(0,0,0,5) };
                strategyPanel.Children.Add(tb);
            }

            // Tags
            strategyPanel.Children.Add(new TextBlock { Text = "【SEOタグ】", FontWeight = FontWeights.Bold, Margin = new Thickness(0,10,0,5) });
            strategyPanel.Children.Add(new TextBox { Text = string.Join(", ", result.Tags), TextWrapping = TextWrapping.Wrap, IsReadOnly = true, Background = Brushes.DarkSlateBlue, Padding = new Thickness(5) });

            ResultPanel.Children.Add(strategyPanel);

            // --- Video Config Card ---
            var card = new Border {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "🎬 生成された構成", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
            stack.Children.Add(new TextBlock { Text = $"フック: {result.Hook}", Foreground = Brushes.Cyan, Margin = new Thickness(0,0,0,10) });
            
            var scriptBox = new TextBox { Text = result.ScriptFull, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Foreground = Brushes.LightGray, BorderThickness = new Thickness(0) };
            stack.Children.Add(scriptBox);

            var exportBtn = new Button {
                Content = "動画とサムネイルを書き出す (v2.0)",
                Style = (Style)FindResource("ModernButton"),
                Height = 45,
                Margin = new Thickness(0, 20, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94))
            };
            exportBtn.Click += async (s, e) => await ExportVideoV2(result);
            stack.Children.Add(exportBtn);

            card.Child = stack;
            ResultPanel.Children.Add(card);
        }

        private async Task ExportVideoV2(V2GenerationResult result)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "v2.6 Premium レンダリング中 (zoompan + loudnorm)...";
            
            try
            {
                // Load SD Model for scenes if needed
                int sdIndex = SdModelCombo.SelectedIndex;
                if (sdIndex >= 0)
                {
                    var sdInfo = _modelManager.SdModels[sdIndex];
                    string sdPath = System.IO.Path.Combine(_modelManager.ModelsDir, sdInfo.Path);
                    if (System.IO.File.Exists(sdPath)) await _sdManager.InitializeAsync(sdPath);
                    _renderer.SetSDManager(_sdManager);
                }

                // 2. Set Voice Model
                int voiceIndex = VoiceModelCombo.SelectedIndex;
                if (voiceIndex >= 0)
                {
                    var voiceInfo = _modelManager.VoiceModels[voiceIndex];
                    string voicePath = Path.Combine(_modelManager.ModelsDir, voiceInfo.Path);
                    if (File.Exists(voicePath)) _renderer.SetVoiceModel(voicePath);
                }

                string outputDir = OutputPathTextBlock.Text;
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string videoPath = Path.Combine(outputDir, $"Shorts_{timestamp}.mp4");
                string thumbPath = Path.Combine(outputDir, $"Thumb_{timestamp}.jpg");

                string selectedBgm = BgmCombo.SelectedItem as string;
                string bgmPath = string.IsNullOrEmpty(selectedBgm) ? "" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BGM", selectedBgm);

                await _renderer.RenderVideoV2Async(result, videoPath, bgmPath);
                await _renderer.RenderThumbnailAsync(result, "", thumbPath);

                MessageBox.Show($"完了しました！\n\n動画: {videoPath}\nサムネ: {thumbPath}", "v2.0 成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"書き出しエラー: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class AppSettings
    {
        public string OutputPath { get; set; } = "";
    }
}