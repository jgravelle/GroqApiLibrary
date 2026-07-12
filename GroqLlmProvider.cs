using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public interface ILlmProvider
    {
        Task<string> GenerateAsync(string prompt);
    }


    public class GroqLlmProvider : ILlmProvider, IDisposable
    {
        private readonly GroqApiClient _client;
        private readonly string _model;
        private readonly IPromptCompressor? _compressor;

        public GroqLlmProvider(string apiKey, string model, IPromptCompressor? compressor = null)
        {
            _client = new GroqApiClient(apiKey);
            _model = model;
            _compressor = compressor;
        }

        public GroqLlmProvider(string apiKey, string model, HttpClient httpClient, IPromptCompressor? compressor = null)
        {
            _client = new GroqApiClient(apiKey, httpClient);
            _model = model;
            _compressor = compressor;
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            // Opt-in prompt compression: when no compressor was supplied, the prompt is sent unchanged.
            if (_compressor != null)
                prompt = await _compressor.CompressAsync(prompt);

            var request = new JsonObject
            {
                ["model"] = _model,
                ["messages"] = JsonSerializer.SerializeToNode(new[]
                {
                new { role = "user", content = prompt }
            })
            };

            var response = await _client.CreateChatCompletionAsync(request);
            return response?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
