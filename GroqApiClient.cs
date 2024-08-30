using System;
using System.Collections.Generic;
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

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

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
            using var reader = new System.IO.StreamReader(stream);
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

        public async Task<JsonObject?> ListModelsAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{BaseUrl}/models");
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject? responseJson = JsonSerializer.Deserialize<JsonObject>(responseString);

            return responseJson;
        }

        public async Task<string> RunConversationWithToolsAsync(string userPrompt, List<Tool> tools, string model, string systemMessage)
        {
            try
            {
                var messages = new List<JsonObject>
                {
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = systemMessage
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = userPrompt
                    }
                };

                var request = new JsonObject
                {
                    ["model"] = model,
                    ["messages"] = JsonSerializer.SerializeToNode(messages),
                    ["tools"] = JsonSerializer.SerializeToNode(tools.Select(t => new
                    {
                        type = t.Type,
                        function = new
                        {
                            name = t.Function.Name,
                            description = t.Function.Description,
                            parameters = t.Function.Parameters
                        }
                    })),
                    ["tool_choice"] = "auto"
                };

                Console.WriteLine($"Sending request to API: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

                var response = await CreateChatCompletionAsync(request);
                var responseMessage = response?["choices"]?[0]?["message"]?.AsObject();
                var toolCalls = responseMessage?["tool_calls"]?.AsArray();

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    messages.Add(responseMessage);
                    foreach (var toolCall in toolCalls)
                    {
                        var functionName = toolCall?["function"]?["name"]?.GetValue<string>();
                        var functionArgs = toolCall?["function"]?["arguments"]?.GetValue<string>();
                        var toolCallId = toolCall?["id"]?.GetValue<string>();

                        if (!string.IsNullOrEmpty(functionName) && !string.IsNullOrEmpty(functionArgs))
                        {
                            var tool = tools.Find(t => t.Function.Name == functionName);
                            if (tool != null)
                            {
                                var functionResponse = await tool.Function.ExecuteAsync(functionArgs);
                                messages.Add(new JsonObject
                                {
                                    ["tool_call_id"] = toolCallId,
                                    ["role"] = "tool",
                                    ["name"] = functionName,
                                    ["content"] = functionResponse
                                });
                            }
                        }
                    }

                    request["messages"] = JsonSerializer.SerializeToNode(messages);
                    var secondResponse = await CreateChatCompletionAsync(request);
                    return secondResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
                }

                return responseMessage?["content"]?.GetValue<string>() ?? string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request error: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }


    public class Tool
    {
        public string Type { get; set; } = "function";
        public Function Function { get; set; }
    }

    public class Function
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JsonObject Parameters { get; set; }
        public Func<string, Task<string>> ExecuteAsync { get; set; }
    }
}