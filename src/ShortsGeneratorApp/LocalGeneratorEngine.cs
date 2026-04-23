using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using Newtonsoft.Json;

namespace ShortsGeneratorApp
{
    public class LocalGeneratorEngine
    {
        private LLamaWeights? _weights;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private string? _currentModelPath;

        public async Task InitializeAsync(string modelPath, int gpuLayers = 10)
        {
            if (_currentModelPath == modelPath && _weights != null) return;

            Dispose();

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 1024, // Reduced context for stability
                GpuLayerCount = gpuLayers // Custom layer count (0 = CPU only)
            };
            
            _weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters));
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
            _currentModelPath = modelPath;
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            if (_executor == null) return "Model not initialized.";

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 1024,
                Temperature = 0.1f, // Make it more deterministic
                AntiPrompts = new List<string> { "User:" }
            };

            string response = "";
            await foreach (var text in _executor.InferAsync(prompt, inferenceParams))
            {
                response += text;
            }
            return response;
        }

        public void Dispose()
        {
            _context?.Dispose();
            _weights?.Dispose();
        }
    }
}
