using System;
using System.Collections.Generic;
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
        private ShortsGeneratorEngine _realEngine;
        private VideoRenderer _renderer = new VideoRenderer();
        private SDManager _sdManager = new SDManager();
        private AppSettings _settings = new AppSettings();
        private string _settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private string _logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_log.txt");

        private void Log(string msg)
        {
            try {
                System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            } catch { }
        }
        
        public MainWindow()
        {
            try {
                InitializeComponent();
                LoadSettings();
                InitializeSettings();
                _localAI = new LocalGeneratorEngine();
                _realEngine = new ShortsGeneratorEngine(_localAI);
                
                // Configure FFMpeg path
                string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (System.IO.File.Exists(ffmpegPath)) {
                    GlobalFFOptions.Configure(options => options.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory);
                }

                this.Title = "Shorts Video Generator PRO v1.7.5 [OFFLINE]";
                
                // Startup check for dependencies
                Dispatcher.BeginInvoke(new Action(async () => {
                    await CheckAndDownloadModelsAsync();
                    await CheckAndDownloadFFmpegAsync();
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
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            string blogText = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(blogText))
            {
                MessageBox.Show("テキストを入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "AIがスクリプトを作成中...";
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
                    var dlResult = MessageBox.Show(
                        $"選択されたモデル ({llmInfo.Name}) が見つかりません。ダウンロードしますか？", 
                        "モデル不足", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (dlResult == MessageBoxResult.Yes)
                    {
                        LoadingProgressBar.IsIndeterminate = false;
                        await _modelManager.DownloadFileAsync(llmInfo.DownloadUrl, llmPath, progress => {
                            Dispatcher.Invoke(() => {
                                LoadingText.Text = $"{llmInfo.Name} をダウンロード中... {progress:F1}%";
                                LoadingProgressBar.Value = progress;
                            });
                        });
                        LoadingProgressBar.IsIndeterminate = true;
                    }
                    else return;
                }

                LoadingText.Text = "AIモデルをロード中...";
                Log($"Initializing AI model: {llmPath}");
                
                int gpuLayers = (UseGpuCheck.IsChecked == true) ? 10 : 0;
                Log($"GPU Layers set to: {gpuLayers} (Mode: {(gpuLayers > 0 ? "GPU" : "CPU")})");
                
                await _localAI.InitializeAsync(llmPath, gpuLayers);

                // 2. Generate Script
                LoadingText.Text = "シナリオを生成中 (AI推論)...";
                Log("Starting AI inference...");
                
                string platform = YoutubeRadio.IsChecked == true ? "YouTube" : "TikTok";
                string orientation = (OrientationCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Vertical (9:16)";
                int duration = (int)DurationSlider.Value;
                int targetChars = (int)TargetCharsSlider.Value;

                var result = await _realEngine.GenerateAsync(platform, blogText, duration, orientation, targetChars);
                
                Log("Generation successful. Displaying results.");
                DisplayResults(result);
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                if (ex.InnerException != null) Log($"INNER ERROR: {ex.InnerException.Message}");
                string errorMsg = $"エラー: {ex.Message}";
                if (ex.InnerException != null) {
                    errorMsg += $"\n内部エラー: {ex.InnerException.Message}";
                }
                MessageBox.Show(errorMsg);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                GenerateButton.IsEnabled = true;
            }
        }

        private void DisplayResults(GenerationResult result)
        {
            foreach (var variation in result.Variations)
            {
                var card = CreateVariationCard(result, variation);
                ResultPanel.Children.Add(card);
            }
        }

        private UIElement CreateVariationCard(GenerationResult result, Variation v)
        {
            // (Re-using the UI logic from previous version but adding an Export button)
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 25, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(20)
            };

            var stack = new StackPanel();

            // Header
            var headerStack = new DockPanel { LastChildFill = true };
            headerStack.Children.Add(new TextBlock { Text = $"Variation {v.VariationId}", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            
            // Export Button
            var exportBtn = new Button
            {
                Content = "動画を書き出す",
                Style = (Style)FindResource("ModernButton"),
                Height = 35,
                Width = 120,
                FontSize = 12,
                Margin = new Thickness(10, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            exportBtn.Click += async (s, e) => {
                await ExportVideo(result, v);
            };
            DockPanel.SetDock(exportBtn, Dock.Right);
            headerStack.Children.Add(exportBtn);

            var refineBtn = new Button
            {
                Content = "AIで文章を洗練させる",
                Style = (Style)FindResource("ModernButton"),
                Height = 35,
                Width = 140,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)) // Blueish
            };
            refineBtn.Click += async (s, e) => {
                await RefineVariationText(result, v);
            };
            DockPanel.SetDock(refineBtn, Dock.Right);
            headerStack.Children.Add(refineBtn);

            stack.Children.Add(headerStack);

            // ... (Rest of the Scene list logic simplified for this update)
            stack.Children.Add(new TextBlock { Text = $"{v.HookType} Hook | {v.TotalDuration}s", Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)), Margin = new Thickness(0, 5, 0, 15) });
            
            foreach (var scene in v.Scenes)
            {
                var scenePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
                
                var sceneHeader = new DockPanel();
                sceneHeader.Children.Add(new TextBlock { Text = $"Scene {scene.Id}", Foreground = Brushes.Gray, FontWeight = FontWeights.Bold });
                
                var removeBtn = new Button { Content = "削除", Width = 50, Height = 20, FontSize = 10, Background = Brushes.Transparent, Foreground = Brushes.Red, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
                removeBtn.Click += (s, e) => {
                    v.Scenes.Remove(scene);
                    ResultPanel.Children.Clear();
                    DisplayResults(result); // Refresh
                };
                DockPanel.SetDock(removeBtn, Dock.Right);
                sceneHeader.Children.Add(removeBtn);
                scenePanel.Children.Add(sceneHeader);

                // Narration
                scenePanel.Children.Add(new TextBlock { Text = "Narration:", FontSize = 10, Foreground = Brushes.DarkGray });
                var narrationBox = new TextBox { Text = scene.Narration, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(5) };
                narrationBox.TextChanged += (s, e) => scene.Narration = narrationBox.Text;
                scenePanel.Children.Add(narrationBox);

                // Telop
                scenePanel.Children.Add(new TextBlock { Text = "Telop Text:", FontSize = 10, Foreground = Brushes.DarkGray, Margin = new Thickness(0, 5, 0, 0) });
                var telopBox = new TextBox { Text = scene.GetTelopText(), Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)), Foreground = Brushes.Yellow, BorderThickness = new Thickness(0), Padding = new Thickness(5) };
                telopBox.TextChanged += (s, e) => {
                    scene.TelopText = telopBox.Text;
                    if (scene.Telop != null) scene.Telop.Text = telopBox.Text;
                };
                scenePanel.Children.Add(telopBox);

                // Visual Prompt
                scenePanel.Children.Add(new TextBlock { Text = "Visual Prompt:", FontSize = 10, Foreground = Brushes.DarkGray, Margin = new Thickness(0, 5, 0, 0) });
                var visualBox = new TextBox { Text = scene.VisualPrompt, Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)), Foreground = Brushes.LightCyan, BorderThickness = new Thickness(0), Padding = new Thickness(5) };
                visualBox.TextChanged += (s, e) => scene.VisualPrompt = visualBox.Text;
                scenePanel.Children.Add(visualBox);

                stack.Children.Add(scenePanel);
            }

            var addBtn = new Button { Content = "+ シーンを追加", Style = (Style)FindResource("ModernButton"), Height = 30, Margin = new Thickness(0, 10, 0, 0) };
            addBtn.Click += (s, e) => {
                v.Scenes.Add(new Scene { 
                    Id = v.Scenes.Count + 1, 
                    Narration = "", 
                    TelopText = "", 
                    VisualPrompt = "A cinematic shot of..." 
                });
                ResultPanel.Children.Clear();
                DisplayResults(result); // Refresh
            };
            stack.Children.Add(addBtn);

            border.Child = stack;
            return border;
        }

        private async Task RefineVariationText(GenerationResult result, Variation v)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "AIで文章を洗練中...";
            
            try
            {
                var refined = await _realEngine.RefineTextAsync(v);
                
                // Update original variation with refined text
                v.Scenes.Clear();
                foreach(var s in refined.Scenes) v.Scenes.Add(s);
                v.TotalDuration = refined.TotalDuration;
                
                ResultPanel.Children.Clear();
                DisplayResults(result); // Refresh UI
            }
            catch (Exception ex)
            {
                MessageBox.Show($"洗練エラー: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ExportVideo(GenerationResult result, Variation v)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "コンポーネントを準備中...";
            
            try
            {
                // Load SD Model if selected
                int sdIndex = SdModelCombo.SelectedIndex;
                if (sdIndex >= 0)
                {
                    var sdInfo = _modelManager.SdModels[sdIndex];
                    string sdPath = System.IO.Path.Combine(_modelManager.ModelsDir, sdInfo.Path);
                    
                    if (System.IO.File.Exists(sdPath))
                    {
                        LoadingText.Text = $"画像生成AIをロード中 ({sdInfo.Name})...";
                        await _sdManager.InitializeAsync(sdPath);
                        _renderer.SetSDManager(_sdManager);
                    }
                    else {
                        var dl = MessageBox.Show($"{sdInfo.Name} が見つかりません。ダウンロードしますか？", "モデル不足", MessageBoxButton.YesNo);
                        if (dl == MessageBoxResult.Yes) {
                            // (Reuse download logic if needed, but for now just skip)
                            MessageBox.Show("モデルを手動でダウンロードして Models フォルダに置いてください。", "通知");
                        }
                    }
                }

                LoadingText.Text = "動画をレンダリング中...\n(音声合成・画像生成・合成)";
                string output = System.IO.Path.Combine(OutputPathTextBlock.Text, $"Video_{v.VariationId}.mp4");
                await _renderer.RenderVideoAsync(result, v, output);
                
                MessageBox.Show($"動画の書き出しが完了しました！\n保存されました: {output}", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
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