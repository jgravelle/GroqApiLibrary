using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public class GroqApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.groq.com/openai/v1";
        private const string ChatCompletionsEndpoint = "/chat/completions";
        private const string TranscriptionsEndpoint = "/audio/transcriptions";
        private const string TranslationsEndpoint = "/audio/translations";

        public GroqApiClient(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<JsonObject?> CreateChatCompletionAsync(JsonObject request)
        {
            var response = await _httpClient.PostAsJsonAsync(BaseUrl + ChatCompletionsEndpoint, request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public async IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request)
        {
            request["stream"] = true;
            var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl + ChatCompletionsEndpoint) { Content = content };
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("data: "))
                {
                    var data = line["data: ".Length..];
                    if (data != "[DONE]")
                    {
                        yield return JsonSerializer.Deserialize<JsonObject>(data);
                    }
                }
            }
        }

        public async Task<JsonObject?> CreateTranscriptionAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", string? language = null, float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(audioFile), "file", fileName);
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (!string.IsNullOrEmpty(language))
                content.Add(new StringContent(language), "language");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString()), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranscriptionsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public async Task<JsonObject?> CreateTranslationAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(audioFile), "file", fileName);
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString()), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranslationsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}