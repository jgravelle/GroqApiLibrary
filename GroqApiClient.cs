using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public class GroqApiClient : IGroqApiClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.groq.com/openai/v1";
        private const string ChatCompletionsEndpoint = "/chat/completions";
        private const string TranscriptionsEndpoint = "/audio/transcriptions";
        private const string TranslationsEndpoint = "/audio/translations";
        private const string SpeechEndpoint = "/audio/speech";
        private const string FilesEndpoint = "/files";
        private const string BatchesEndpoint = "/batches";

        // Vision-capable models. Verified against console.groq.com/docs/models &amp; /deprecations (2026-07-12).
        // qwen/qwen3.6-27b is the current recommended vision model. The Llama 4 / Llama 3.2 vision
        // models are deprecated (Maverick decommissioned 2026-03-09, Scout shuts down 2026-07-17,
        // Llama 3.2 vision long gone) but are retained here so existing callers still pass validation
        // during the transition. Note: openai/gpt-oss-120b is text-only and is intentionally NOT listed.
        private static readonly HashSet<string> VisionModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "qwen/qwen3.6-27b",
            "meta-llama/llama-4-scout-17b-16e-instruct",
            "meta-llama/llama-4-maverick-17b-128e-instruct",
            "llama-3.2-90b-vision-preview",
            "llama-3.2-11b-vision-preview"
        };

        private const int MAX_IMAGE_SIZE_MB = 20;
        private const int MAX_BASE64_SIZE_MB = 4;

        public GroqApiClient(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Creates a GroqApiClient with an existing HttpClient instance.
        /// Useful for dependency injection or when you need custom HttpClient configuration.
        /// </summary>
        public GroqApiClient(string apiKey, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Sets custom headers for requests (e.g., Groq-Model-Version for compound systems)
        /// </summary>
        public void SetCustomHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.Add(name, value);
        }

        private async Task<string> ConvertImageToBase64(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            return Convert.ToBase64String(imageBytes);
        }

        #region Chat Completions

        /// <summary>
        /// Groq does not support Structured Outputs (response_format: json_schema) together with
        /// streaming or tool use. Callers previously only found out via an API error; this catches
        /// it client-side with a clearer message. Verified against console.groq.com/docs/structured-outputs
        /// (2026-07-12): "Streaming and tool use are not currently supported with Structured Outputs."
        /// </summary>
        private static void ValidateStructuredOutputCompatibility(JsonObject request, bool streaming)
        {
            var responseFormat = request["response_format"] as JsonObject;
            if (responseFormat?["type"]?.GetValue<string>() != "json_schema")
                return;

            var isStreaming = streaming || (request["stream"]?.GetValue<bool>() ?? false);
            var hasTools = request["tools"] is JsonArray { Count: > 0 };

            if (!isStreaming && !hasTools)
                return;

            var reasons = new List<string>();
            if (isStreaming) reasons.Add("streaming");
            if (hasTools) reasons.Add("tool use");
            throw new ArgumentException(
                $"Groq does not support Structured Outputs (response_format: json_schema) combined with {string.Join(" or ", reasons)}. " +
                "Remove response_format, or drop streaming/tools for this request.");
        }

        public async Task<JsonObject?> CreateChatCompletionAsync(JsonObject request)
        {
            ValidateStructuredOutputCompatibility(request, streaming: false);

            var response = await _httpClient.PostAsJsonAsync(BaseUrl + ChatCompletionsEndpoint, request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Creates a chat completion from messages plus optional strongly-typed request parameters
        /// (see <see cref="GroqChatOptions"/>). Read token usage/analytics from the response with
        /// <see cref="GroqUsage.FromResponse"/>.
        /// </summary>
        public Task<JsonObject?> CreateChatCompletionAsync(JsonArray messages, string model, GroqChatOptions? options = null)
        {
            var request = new JsonObject { ["model"] = model, ["messages"] = messages };
            options?.ApplyTo(request);
            return CreateChatCompletionAsync(request);
        }

        public async IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request)
        {
            ValidateStructuredOutputCompatibility(request, streaming: true);

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

        /// <summary>
        /// Streams a chat completion from messages plus optional strongly-typed parameters
        /// (see <see cref="GroqChatOptions"/>). Set <see cref="GroqChatOptions.StreamIncludeUsage"/> to
        /// receive a final chunk carrying <c>usage</c> (read it with <see cref="GroqUsage.FromResponse"/>).
        /// </summary>
        public IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonArray messages, string model, GroqChatOptions? options = null)
        {
            var request = new JsonObject { ["model"] = model, ["messages"] = messages };
            options?.ApplyTo(request);
            return CreateChatCompletionStreamAsync(request);
        }

        /// <summary>
        /// Creates a chat completion with structured output using JSON Schema.
        /// Supports strict mode for guaranteed schema compliance on supported models (GPT-OSS 20B/120B).
        /// </summary>
        public async Task<JsonObject?> CreateChatCompletionWithStructuredOutputAsync(
            JsonArray messages,
            string model,
            JsonObject jsonSchema,
            string schemaName = "response",
            bool strict = false,
            float? temperature = null,
            int? maxCompletionTokens = null)
        {
            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["response_format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JsonObject
                    {
                        ["name"] = schemaName,
                        ["strict"] = strict,
                        ["schema"] = jsonSchema
                    }
                }
            };

            if (temperature.HasValue)
                request["temperature"] = temperature.Value;

            if (maxCompletionTokens.HasValue)
                request["max_completion_tokens"] = maxCompletionTokens.Value;

            return await CreateChatCompletionAsync(request);
        }

        /// <summary>
        /// Creates a chat completion with reasoning/thinking support.
        /// For Qwen3 models: use reasoningEffort = "none" to disable, "default" to enable.
        /// For GPT-OSS models: use reasoningEffort = "low", "medium", or "high".
        /// </summary>
        public async Task<JsonObject?> CreateChatCompletionWithReasoningAsync(
            JsonArray messages,
            string model,
            string? reasoningEffort = null,
            string? reasoningFormat = null,
            bool? includeReasoning = null,
            float? temperature = null,
            int? maxCompletionTokens = null)
        {
            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = messages
            };

            if (!string.IsNullOrEmpty(reasoningEffort))
                request["reasoning_effort"] = reasoningEffort;

            if (!string.IsNullOrEmpty(reasoningFormat))
                request["reasoning_format"] = reasoningFormat;

            if (includeReasoning.HasValue)
                request["include_reasoning"] = includeReasoning.Value;

            if (temperature.HasValue)
                request["temperature"] = temperature.Value;

            if (maxCompletionTokens.HasValue)
                request["max_completion_tokens"] = maxCompletionTokens.Value;

            return await CreateChatCompletionAsync(request);
        }

        /// <summary>
        /// Creates a chat completion with document context (for RAG-style applications).
        /// </summary>
        public async Task<JsonObject?> CreateChatCompletionWithDocumentsAsync(
            JsonArray messages,
            string model,
            JsonArray documents,
            bool enableCitations = true,
            float? temperature = null,
            int? maxCompletionTokens = null)
        {
            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["documents"] = documents,
                ["citation_options"] = enableCitations ? "enabled" : "disabled"
            };

            if (temperature.HasValue)
                request["temperature"] = temperature.Value;

            if (maxCompletionTokens.HasValue)
                request["max_completion_tokens"] = maxCompletionTokens.Value;

            return await CreateChatCompletionAsync(request);
        }

        /// <summary>
        /// Creates a chat completion using Compound systems (groq/compound or groq/compound-mini)
        /// with built-in server-side tools (web search, code execution, visit website, Wolfram Alpha).
        /// The response includes an <c>executed_tools</c> array (which tools ran) and a
        /// <c>usage_breakdown</c> object (per-underlying-model tokens); use
        /// <see cref="GetExecutedTools"/> to read the former.
        /// </summary>
        /// <param name="enabledTools">
        /// Optional allow-list of built-in tools to enable (e.g. "web_search", "code_interpreter",
        /// "visit_website", "wolfram_alpha"), sent as compound_custom.tools.enabled_tools. When null,
        /// the system default set is used.
        /// </param>
        public async Task<JsonObject?> CreateCompoundCompletionAsync(
            JsonArray messages,
            bool useMini = false,
            SearchSettings? searchSettings = null,
            float? temperature = null,
            int? maxCompletionTokens = null,
            string[]? enabledTools = null)
        {
            var request = new JsonObject
            {
                ["model"] = useMini ? "groq/compound-mini" : "groq/compound",
                ["messages"] = messages
            };

            if (searchSettings != null)
            {
                var settings = new JsonObject();
                if (searchSettings.IncludeDomains?.Length > 0)
                    settings["include_domains"] = JsonSerializer.SerializeToNode(searchSettings.IncludeDomains);
                if (searchSettings.ExcludeDomains?.Length > 0)
                    settings["exclude_domains"] = JsonSerializer.SerializeToNode(searchSettings.ExcludeDomains);
                request["search_settings"] = settings;
            }

            if (enabledTools is { Length: > 0 })
            {
                var tools = new JsonArray();
                foreach (var t in enabledTools) tools.Add(t);
                request["compound_custom"] = new JsonObject
                {
                    ["tools"] = new JsonObject { ["enabled_tools"] = tools }
                };
            }

            if (temperature.HasValue)
                request["temperature"] = temperature.Value;

            if (maxCompletionTokens.HasValue)
                request["max_completion_tokens"] = maxCompletionTokens.Value;

            return await CreateChatCompletionAsync(request);
        }

        /// <summary>
        /// Returns the <c>executed_tools</c> array from a Compound response (the built-in tools the
        /// system ran while producing the answer), or null if none is present.
        /// </summary>
        public static JsonArray? GetExecutedTools(JsonObject? response)
            => response?["choices"]?[0]?["message"]?["executed_tools"] as JsonArray;

        #endregion

        #region Audio Transcription & Translation

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
                content.Add(new StringContent(temperature.Value.ToString(CultureInfo.InvariantCulture)), "temperature");

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
                content.Add(new StringContent(temperature.Value.ToString(CultureInfo.InvariantCulture)), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranslationsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Transcribes audio from a publicly accessible URL, avoiding an upload. Alternative to the
        /// multipart <see cref="CreateTranscriptionAsync"/>.
        /// </summary>
        public async Task<JsonObject?> CreateTranscriptionFromUrlAsync(string url, string model,
            string? prompt = null, string responseFormat = "json", string? language = null, float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(url), "url");
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (!string.IsNullOrEmpty(language))
                content.Add(new StringContent(language), "language");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString(CultureInfo.InvariantCulture)), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranscriptionsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Translates audio from a publicly accessible URL to English, avoiding an upload. Alternative
        /// to the multipart <see cref="CreateTranslationAsync"/>.
        /// </summary>
        public async Task<JsonObject?> CreateTranslationFromUrlAsync(string url, string model,
            string? prompt = null, string responseFormat = "json", float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(url), "url");
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString(CultureInfo.InvariantCulture)), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranslationsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        #endregion

        #region Text-to-Speech

        /// <summary>
        /// Converts text to speech audio using Orpheus TTS models.
        /// </summary>
        /// <param name="text">The text to convert to speech (max 200 characters). English supports vocal directions in brackets like [cheerful], [whisper], [dramatic].</param>
        /// <param name="voice">Voice to use. English (orpheus-v1-english): autumn, diana, hannah, austin, daniel, troy. Arabic (orpheus-arabic-saudi): abdullah, fahad, sultan, lulwa, noura, aisha. See <see cref="OrpheusVoices"/>.</param>
        /// <param name="model">TTS model: canopylabs/orpheus-v1-english or canopylabs/orpheus-arabic-saudi</param>
        /// <param name="responseFormat">Audio format. Orpheus only supports "wav"; other values are not accepted by the API.</param>
        /// <returns>Audio bytes (WAV)</returns>
        public async Task<byte[]> CreateSpeechAsync(
            string text,
            string voice,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav")
        {
            var request = new JsonObject
            {
                ["model"] = model,
                ["input"] = text,
                ["voice"] = voice,
                ["response_format"] = responseFormat
            };

            var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(BaseUrl + SpeechEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"TTS request failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Converts text to speech and saves directly to a file.
        /// </summary>
        public async Task CreateSpeechToFileAsync(
            string text,
            string voice,
            string outputPath,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav")
        {
            var audioBytes = await CreateSpeechAsync(text, voice, model, responseFormat);
            await File.WriteAllBytesAsync(outputPath, audioBytes);
        }

        /// <summary>
        /// Converts text to speech and returns as a stream.
        /// </summary>
        public async Task<Stream> CreateSpeechStreamAsync(
            string text,
            string voice,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav")
        {
            var request = new JsonObject
            {
                ["model"] = model,
                ["input"] = text,
                ["voice"] = voice,
                ["response_format"] = responseFormat
            };

            var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(BaseUrl + SpeechEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"TTS request failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadAsStreamAsync();
        }

        #endregion

        #region Vision

        public async Task<JsonObject?> CreateVisionCompletionAsync(JsonObject request)
        {
            ValidateVisionModel(request);
            return await CreateChatCompletionAsync(request);
        }

        public async Task<JsonObject?> CreateVisionCompletionWithImageUrlAsync(
            string imageUrl,
            string prompt,
            string model = "qwen/qwen3.6-27b",
            float? temperature = null)
        {
            ValidateImageUrl(imageUrl);

            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = prompt
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = imageUrl
                                }
                            }
                        }
                    }
                }
            };

            if (temperature.HasValue)
            {
                request["temperature"] = temperature.Value;
            }

            return await CreateVisionCompletionAsync(request);
        }

        public async Task<JsonObject?> CreateVisionCompletionWithBase64ImageAsync(
            string imagePath,
            string prompt,
            string model = "qwen/qwen3.6-27b",
            float? temperature = null)
        {
            var base64Image = await ConvertImageToBase64(imagePath);
            ValidateBase64Size(base64Image);

            var mimeType = GetMimeType(imagePath);
            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = prompt
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = $"data:{mimeType};base64,{base64Image}"
                                }
                            }
                        }
                    }
                }
            };

            if (temperature.HasValue)
            {
                request["temperature"] = temperature.Value;
            }

            return await CreateVisionCompletionAsync(request);
        }

        public async Task<JsonObject?> CreateVisionCompletionWithToolsAsync(
            string imageUrl,
            string prompt,
            List<Tool> tools,
            string model = "qwen/qwen3.6-27b",
            bool parallelToolCalls = true)
        {
            ValidateImageUrl(imageUrl);

            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = prompt
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = imageUrl
                                }
                            }
                        }
                    }
                },
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
                ["tool_choice"] = "auto",
                ["parallel_tool_calls"] = parallelToolCalls
            };

            return await CreateVisionCompletionAsync(request);
        }

        public async Task<JsonObject?> CreateVisionCompletionWithJsonModeAsync(
            string imageUrl,
            string prompt,
            string model = "qwen/qwen3.6-27b")
        {
            ValidateImageUrl(imageUrl);

            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = prompt
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = imageUrl
                                }
                            }
                        }
                    }
                },
                ["response_format"] = new JsonObject { ["type"] = "json_object" }
            };

            return await CreateVisionCompletionAsync(request);
        }

        /// <summary>
        /// Creates a vision completion with structured JSON output.
        /// </summary>
        public async Task<JsonObject?> CreateVisionCompletionWithStructuredOutputAsync(
            string imageUrl,
            string prompt,
            JsonObject jsonSchema,
            string schemaName = "response",
            bool strict = false,
            string model = "qwen/qwen3.6-27b")
        {
            ValidateImageUrl(imageUrl);

            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = prompt
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = imageUrl
                                }
                            }
                        }
                    }
                },
                ["response_format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JsonObject
                    {
                        ["name"] = schemaName,
                        ["strict"] = strict,
                        ["schema"] = jsonSchema
                    }
                }
            };

            return await CreateVisionCompletionAsync(request);
        }

        #endregion

        #region Models

        public async Task<JsonObject?> ListModelsAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{BaseUrl}/models");
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject? responseJson = JsonSerializer.Deserialize<JsonObject>(responseString);

            return responseJson;
        }

        #endregion

        #region Files

        // The Files and Batch APIs require an eligible Groq plan; on unsupported plans the API
        // returns 403 with code "not_available_for_plan".

        /// <summary>
        /// Uploads a file for use with the Batch API. Files must be JSONL and up to 100 MB.
        /// Requires a Groq plan that includes the Files/Batch API.
        /// </summary>
        /// <param name="file">The file content stream.</param>
        /// <param name="fileName">File name (e.g. "batch_input.jsonl").</param>
        /// <param name="purpose">Upload purpose. Currently only "batch" is supported.</param>
        /// <returns>The created file object: id, object, bytes, created_at, filename, purpose.</returns>
        public async Task<JsonObject?> UploadFileAsync(Stream file, string fileName, string purpose = "batch")
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(file), "file", fileName);
            content.Add(new StringContent(purpose), "purpose");

            var response = await _httpClient.PostAsync(BaseUrl + FilesEndpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"File upload failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Lists the files owned by the account. Returns { object: "list", data: [ ... ] }.
        /// </summary>
        public async Task<JsonObject?> ListFilesAsync()
        {
            var response = await _httpClient.GetAsync(BaseUrl + FilesEndpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Retrieves metadata for a single file.
        /// </summary>
        public async Task<JsonObject?> GetFileAsync(string fileId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{FilesEndpoint}/{fileId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Downloads the raw content of a file (e.g. batch output JSONL) as bytes.
        /// </summary>
        public async Task<byte[]> GetFileContentAsync(string fileId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{FilesEndpoint}/{fileId}/content");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"File content download failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Downloads the raw content of a file as a stream (e.g. for large batch outputs).
        /// </summary>
        public async Task<Stream> GetFileContentStreamAsync(string fileId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{FilesEndpoint}/{fileId}/content", HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Deletes a file. Returns { id, object, deleted }.
        /// </summary>
        public async Task<JsonObject?> DeleteFileAsync(string fileId)
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}{FilesEndpoint}/{fileId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        #endregion

        #region Batch

        // Like Files, the Batch API requires an eligible Groq plan (else 403 not_available_for_plan).
        // Batches run asynchronously (50% discount) and have their own rate limits; they do not
        // accept a service_tier. See BatchRequestBuilder / BatchJsonl / GroqBatch for helpers.

        /// <summary>
        /// Creates a batch job that processes an uploaded JSONL input file asynchronously.
        /// </summary>
        /// <param name="inputFileId">Id of an uploaded file (see <see cref="UploadFileAsync"/>).</param>
        /// <param name="endpoint">Target endpoint for every line, e.g. <see cref="BatchEndpoints.ChatCompletions"/>.</param>
        /// <param name="completionWindow">Processing window from "24h" to "7d".</param>
        /// <param name="metadata">Optional caller metadata echoed back on the batch object.</param>
        public async Task<JsonObject?> CreateBatchAsync(string inputFileId, string endpoint,
            string completionWindow = "24h", JsonObject? metadata = null)
        {
            var request = new JsonObject
            {
                ["input_file_id"] = inputFileId,
                ["endpoint"] = endpoint,
                ["completion_window"] = completionWindow
            };
            if (metadata is not null)
                request["metadata"] = JsonNode.Parse(metadata.ToJsonString());

            var response = await _httpClient.PostAsJsonAsync(BaseUrl + BatchesEndpoint, request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Batch creation failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Retrieves a batch by id. Parse with <see cref="GroqBatch.FromResponse"/> to poll status.
        /// </summary>
        public async Task<JsonObject?> GetBatchAsync(string batchId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}{BatchesEndpoint}/{batchId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Lists batch jobs. Returns { object: "list", data: [ ... ] }.
        /// </summary>
        public async Task<JsonObject?> ListBatchesAsync()
        {
            var response = await _httpClient.GetAsync(BaseUrl + BatchesEndpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        /// <summary>
        /// Requests cancellation of an in-progress batch.
        /// </summary>
        public async Task<JsonObject?> CancelBatchAsync(string batchId)
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}{BatchesEndpoint}/{batchId}/cancel", null);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Batch cancel failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        #endregion

        #region Tool Use

        public async Task<string> RunConversationWithToolsAsync(
            string userPrompt, 
            List<Tool> tools, 
            string model, 
            string systemMessage,
            bool parallelToolCalls = true,
            string? serviceTier = null)
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
                    ["tool_choice"] = "auto",
                    ["parallel_tool_calls"] = parallelToolCalls
                };

                if (!string.IsNullOrEmpty(serviceTier))
                    request["service_tier"] = serviceTier;

                var response = await CreateChatCompletionAsync(request);
                var responseMessage = response?["choices"]?[0]?["message"]?.AsObject();
                var toolCalls = responseMessage?["tool_calls"]?.AsArray();

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    messages.Add(responseMessage!);
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

        #endregion

        #region Validation Helpers

        private void ValidateVisionModel(JsonObject request)
        {
            var model = request["model"]?.GetValue<string>();
            if (string.IsNullOrEmpty(model) || !VisionModels.Contains(model))
            {
                throw new ArgumentException($"Invalid vision model. Must be one of: {string.Join(", ", VisionModels)}");
            }
        }

        private void ValidateBase64Size(string base64String)
        {
            double sizeInMB = (base64String.Length * 3.0 / 4.0) / (1024 * 1024);
            if (sizeInMB > MAX_BASE64_SIZE_MB)
                throw new ArgumentException($"Base64 encoded image exceeds maximum size of {MAX_BASE64_SIZE_MB}MB");
        }

        private void ValidateImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("Image URL cannot be null or empty");

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                throw new ArgumentException("Invalid image URL format");
        }

        private static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        #endregion

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    #region Supporting Types

    /// <summary>
    /// Settings for web search in Compound systems
    /// </summary>
    public class SearchSettings
    {
        /// <summary>
        /// Domains to include in search results
        /// </summary>
        public string[]? IncludeDomains { get; set; }

        /// <summary>
        /// Domains to exclude from search results
        /// </summary>
        public string[]? ExcludeDomains { get; set; }
    }

    /// <summary>
    /// Known model identifiers for convenience.
    /// Verified against console.groq.com/docs/models and /deprecations as of 2026-07-12.
    /// Members marked [Obsolete] are deprecated or already decommissioned on GroqCloud;
    /// the message names the current replacement.
    /// </summary>
    public static class GroqModels
    {
        // ----- Recommended current models -----

        // GPT-OSS: open-weight, reasoning + built-in tools, structured outputs with strict mode.
        // GptOss120B is the recommended default chat model. NOTE: text-only (not vision-capable).
        public const string GptOss120B = "openai/gpt-oss-120b";
        public const string GptOss20B = "openai/gpt-oss-20b";
        public const string GptOssSafeguard20B = "openai/gpt-oss-safeguard-20b";

        // Qwen 3.6 27B: current recommended reasoning + vision-capable model (131K context).
        public const string Qwen36_27B = "qwen/qwen3.6-27b";

        // Allam 2 7B: Arabic-focused language model.
        public const string Allam2_7B = "allam-2-7b";

        // Compound agentic systems (used via the chat endpoint by setting the model).
        public const string Compound = "groq/compound";
        public const string CompoundMini = "groq/compound-mini";

        // Guard / safety models.
        public const string LlamaGuard4 = "meta-llama/llama-guard-4-12b";
        public const string PromptGuard22M = "meta-llama/llama-prompt-guard-2-22m";
        public const string PromptGuard86M = "meta-llama/llama-prompt-guard-2-86m";

        // Audio (speech-to-text).
        public const string WhisperLargeV3 = "whisper-large-v3";
        public const string WhisperLargeV3Turbo = "whisper-large-v3-turbo";

        // Text-to-speech (Orpheus).
        public const string OrpheusEnglish = "canopylabs/orpheus-v1-english";
        public const string OrpheusArabic = "canopylabs/orpheus-arabic-saudi";

        // ----- Deprecated: still served for now, but scheduled for shutdown -----

        [Obsolete("Deprecated on GroqCloud; shuts down 2026-08-16. Use GptOss20B.")]
        public const string Llama31_8B = "llama-3.1-8b-instant";
        [Obsolete("Deprecated on GroqCloud; shuts down 2026-08-16. Use GptOss120B or Qwen36_27B.")]
        public const string Llama33_70B = "llama-3.3-70b-versatile";
        [Obsolete("Deprecated on GroqCloud; shuts down 2026-07-17. Use Qwen36_27B (vision) or GptOss120B.")]
        public const string Llama4Scout = "meta-llama/llama-4-scout-17b-16e-instruct";
        [Obsolete("Deprecated on GroqCloud; shuts down 2026-07-17. Use GptOss120B.")]
        public const string Qwen3_32B = "qwen/qwen3-32b";

        // ----- Decommissioned: no longer served by GroqCloud (calls will fail) -----

        [Obsolete("DECOMMISSIONED 2026-04-15 - no longer served. Use GptOss120B.")]
        public const string KimiK2 = "moonshotai/kimi-k2-instruct-0905";
        [Obsolete("DECOMMISSIONED 2026-03-09 - no longer served. Use GptOss120B.")]
        public const string Llama4Maverick = "meta-llama/llama-4-maverick-17b-128e-instruct";
        [Obsolete("DECOMMISSIONED - llama-3.2 vision no longer served. Use Qwen36_27B for vision.")]
        public const string Llama32_90BVision = "llama-3.2-90b-vision-preview";
        [Obsolete("DECOMMISSIONED - llama-3.2 vision no longer served. Use Qwen36_27B for vision.")]
        public const string Llama32_11BVision = "llama-3.2-11b-vision-preview";
    }

    /// <summary>
    /// Known TTS voices for Orpheus models.
    /// Verified against console.groq.com/docs/text-to-speech/orpheus as of 2026-07-12.
    /// </summary>
    public static class OrpheusVoices
    {
        // English voices (canopylabs/orpheus-v1-english)
        public const string Autumn = "autumn";
        public const string Diana = "diana";
        public const string Hannah = "hannah";
        public const string Austin = "austin";
        public const string Daniel = "daniel";
        public const string Troy = "troy";

        // Arabic voices (canopylabs/orpheus-arabic-saudi)
        public const string Abdullah = "abdullah";
        public const string Fahad = "fahad";
        public const string Sultan = "sultan";
        public const string Lulwa = "lulwa";
        public const string Noura = "noura";
        public const string Aisha = "aisha";
    }

    /// <summary>
    /// Service tier options for controlling request priority
    /// </summary>
    public static class ServiceTiers
    {
        public const string Auto = "auto";
        public const string OnDemand = "on_demand";
        public const string Flex = "flex";
        public const string Performance = "performance";
    }

    /// <summary>
    /// Reasoning effort levels for models that support thinking/reasoning
    /// </summary>
    public static class ReasoningEffort
    {
        // For Qwen3 models
        public const string None = "none";
        public const string Default = "default";
        
        // For GPT-OSS models
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
    }

    /// <summary>
    /// Reasoning format options
    /// </summary>
    public static class ReasoningFormat
    {
        public const string Hidden = "hidden";
        public const string Raw = "raw";
        public const string Parsed = "parsed";
    }

    public class Tool
    {
        public string Type { get; set; } = "function";
        public Function Function { get; set; } = null!;
    }

    public class Function
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public JsonObject Parameters { get; set; } = null!;
        public Func<string, Task<string>> ExecuteAsync { get; set; } = null!;
    }

    #endregion
}
