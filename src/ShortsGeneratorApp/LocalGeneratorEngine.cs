using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace ShortsGeneratorApp
{
    public class LocalGeneratorEngine : IDisposable
    {
        private LLamaWeights? _weights;
        private string? _currentModelPath;
        private int _lastGpuLayers = 10;

        public async Task InitializeAsync(string modelPath, int gpuLayers = 10)
        {
            if (_currentModelPath == modelPath && _weights != null && _lastGpuLayers == gpuLayers) return;

            Dispose();

            _lastGpuLayers = gpuLayers;
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 4096, 
                GpuLayerCount = gpuLayers 
            };
            
            _weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters));
            _currentModelPath = modelPath;
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            if (_weights == null || string.IsNullOrEmpty(_currentModelPath)) 
                return "Model not initialized.";

            // Use StatelessExecutor to prevent context build-up (Fixes NoKvSlot)
            var parameters = new ModelParams(_currentModelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = _lastGpuLayers
            };

            var executor = new StatelessExecutor(_weights, parameters);
            
            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 2048,
                Temperature = 0.7f,
                TopP = 0.95f,
                AntiPrompts = new List<string> { "User:", "[INPUT DATA END]" }
            };

            string response = "";
            try {
                await foreach (var text in executor.InferAsync(prompt, inferenceParams))
                {
                    response += text;
                }
            } catch (Exception ex) {
                return $"[Error] AI推論中にエラーが発生しました: {ex.Message}";
            }
            
            return response;
        }

        public void Dispose()
        {
            _weights?.Dispose();
            _weights = null;
        }
    }
}
