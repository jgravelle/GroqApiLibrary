# Groq API C# Client Library

Welcome to the Groq API C# Client Library! This powerful and flexible library provides a comprehensive interface to interact with the Groq AI API. Designed for .NET 8 and above, our library offers a full range of features to enhance your AI-powered applications.

## 🌟 Features

- 💬 **Chat Completions**: Standard and streaming conversations with AI models
- 🎯 **Structured Outputs**: Guaranteed JSON Schema compliance with strict mode
- 🧠 **Reasoning Models**: Support for thinking/reasoning with Qwen3 and GPT-OSS
- 🔊 **Audio Transcription**: Convert speech to text with Whisper
- 🌐 **Audio Translation**: Translate audio content across languages
- 🗣️ **Text-to-Speech**: Generate lifelike audio with Orpheus TTS
- 👁️ **Vision Analysis**: Process images with Llama 4 multimodal models
- 🛠️ **Tool/Function Calling**: Extend AI capabilities with custom tools
- 🔍 **Compound Systems**: Built-in web search and code execution
- 📄 **Document Context**: RAG-style completions with citations
- 📋 **Model Listing**: Retrieve available AI models

## 📦 Installation

### NuGet Package
```bash
dotnet add package GroqApiLibrary
```

### Manual Installation
1. Clone this repository or download the source files
2. Add the files to your project
3. Ensure your project targets .NET 8 or later

## 🚀 Quick Start

```csharp
using GroqApiLibrary;
using System.Text.Json.Nodes;

var apiKey = "your_api_key_here";
var groqApi = new GroqApiClient(apiKey);

var request = new JsonObject
{
    ["model"] = GroqModels.Llama33_70B,
    ["messages"] = new JsonArray
    {
        new JsonObject
        {
            ["role"] = "user",
            ["content"] = "Hello, Groq! What can you do?"
        }
    }
};

var result = await groqApi.CreateChatCompletionAsync(request);
Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

## 📚 Detailed Usage

### Chat Completions

#### Standard Chat Completion

```csharp
var request = new JsonObject
{
    ["model"] = GroqModels.Llama33_70B,
    ["temperature"] = 0.7,
    ["max_completion_tokens"] = 150,
    ["messages"] = new JsonArray
    {
        new JsonObject
        {
            ["role"] = "system",
            ["content"] = "You are a helpful assistant."
        },
        new JsonObject
        {
            ["role"] = "user",
            ["content"] = "Write a haiku about artificial intelligence."
        }
    }
};

var result = await groqApi.CreateChatCompletionAsync(request);
Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

#### Streaming Chat Completion

```csharp
var request = new JsonObject
{
    ["model"] = GroqModels.Llama33_70B,
    ["messages"] = new JsonArray
    {
        new JsonObject
        {
            ["role"] = "user",
            ["content"] = "Explain quantum entanglement."
        }
    }
};

await foreach (var chunk in groqApi.CreateChatCompletionStreamAsync(request))
{
    var delta = chunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty;
    Console.Write(delta);
}
```

### Structured Outputs (JSON Schema)

Guarantee model responses conform to your JSON schema. Use `strict: true` for 100% reliability with GPT-OSS models.

```csharp
var messages = new JsonArray
{
    new JsonObject
    {
        ["role"] = "system",
        ["content"] = "Extract product review information from the text."
    },
    new JsonObject
    {
        ["role"] = "user",
        ["content"] = "I bought the UltraSound Headphones and I'm impressed! Great noise cancellation. 4.5 out of 5 stars."
    }
};

var schema = new JsonObject
{
    ["type"] = "object",
    ["properties"] = new JsonObject
    {
        ["product_name"] = new JsonObject { ["type"] = "string" },
        ["rating"] = new JsonObject { ["type"] = "number" },
        ["sentiment"] = new JsonObject 
        { 
            ["type"] = "string",
            ["enum"] = new JsonArray { "positive", "negative", "neutral" }
        },
        ["key_features"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string" }
        }
    },
    ["required"] = new JsonArray { "product_name", "rating", "sentiment", "key_features" },
    ["additionalProperties"] = false
};

var result = await groqApi.CreateChatCompletionWithStructuredOutputAsync(
    messages,
    GroqModels.GptOss20B,  // Supports strict: true
    schema,
    schemaName: "product_review",
    strict: true
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

> **Note:** Groq does not support Structured Outputs (`response_format` of type `json_schema`) together with **streaming** or **tool use**. The client guards against this: a request combining `json_schema` with `stream` or `tools` throws an `ArgumentException` up front (rather than failing server-side). If you need those features, use JSON object mode (`response_format` type `json_object`) instead.

### Reasoning/Thinking Models

Enable reasoning for more thoughtful responses with Qwen3 or GPT-OSS models.

```csharp
var messages = new JsonArray
{
    new JsonObject
    {
        ["role"] = "user",
        ["content"] = "What's 15% of 847? Show your reasoning."
    }
};

// For Qwen3: use "none" to disable, "default" to enable
// For GPT-OSS: use "low", "medium", or "high"
var result = await groqApi.CreateChatCompletionWithReasoningAsync(
    messages,
    GroqModels.GptOss20B,
    reasoningEffort: ReasoningEffort.Medium,
    reasoningFormat: ReasoningFormat.Parsed
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

### Compound Systems (Web Search & Code Execution)

Use Groq's Compound systems for real-time information and code execution without any setup.

```csharp
var messages = new JsonArray
{
    new JsonObject
    {
        ["role"] = "user",
        ["content"] = "What are the top tech news stories today?"
    }
};

var result = await groqApi.CreateCompoundCompletionAsync(
    messages,
    useMini: false,  // Use groq/compound for multiple tool calls
    searchSettings: new SearchSettings
    {
        IncludeDomains = new[] { "techcrunch.com", "theverge.com", "arstechnica.com" }
    }
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());

// View which tools were used
var executedTools = result?["choices"]?[0]?["message"]?["executed_tools"];
Console.WriteLine($"Tools used: {executedTools}");
```

### Text-to-Speech (Orpheus TTS)

Generate lifelike audio from text with support for vocal directions.

```csharp
// Basic TTS
var audioBytes = await groqApi.CreateSpeechAsync(
    text: "Welcome to Groq! This is text-to-speech in action.",
    voice: OrpheusVoices.Tara,
    model: GroqModels.OrpheusEnglish,
    responseFormat: "wav"
);
await File.WriteAllBytesAsync("output.wav", audioBytes);

// With vocal directions
var expressiveAudio = await groqApi.CreateSpeechAsync(
    text: "[cheerful] Great news everyone! [serious] But first, let me explain the details.",
    voice: OrpheusVoices.Leo,
    model: GroqModels.OrpheusEnglish
);

// Save directly to file
await groqApi.CreateSpeechToFileAsync(
    text: "This saves directly to a file.",
    voice: OrpheusVoices.Mia,
    outputPath: "speech.wav"
);

// Arabic TTS
var arabicAudio = await groqApi.CreateSpeechAsync(
    text: "مرحبا بكم في جروك",
    voice: OrpheusVoices.Abdullah,
    model: GroqModels.OrpheusArabic
);
```

### Vision Analysis

Process and analyze images with Llama 4 multimodal models.

```csharp
// Analyze image from URL
var result = await groqApi.CreateVisionCompletionWithImageUrlAsync(
    imageUrl: "https://example.com/image.jpg",
    prompt: "What's in this image? Describe it in detail.",
    model: GroqModels.Llama4Scout
);
Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());

// Analyze local image
var localResult = await groqApi.CreateVisionCompletionWithBase64ImageAsync(
    imagePath: "path/to/local/image.jpg",
    prompt: "Describe this image",
    model: GroqModels.Llama4Maverick  // Higher capacity model
);

// Vision with structured output
var schema = new JsonObject
{
    ["type"] = "object",
    ["properties"] = new JsonObject
    {
        ["objects"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string" }
        },
        ["scene_type"] = new JsonObject { ["type"] = "string" },
        ["dominant_colors"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string" }
        }
    },
    ["required"] = new JsonArray { "objects", "scene_type", "dominant_colors" },
    ["additionalProperties"] = false
};

var structuredResult = await groqApi.CreateVisionCompletionWithStructuredOutputAsync(
    imageUrl: "https://example.com/photo.jpg",
    prompt: "Analyze this image and extract the requested information.",
    jsonSchema: schema,
    schemaName: "image_analysis"
);
```

### Audio Transcription

```csharp
using var audioStream = File.OpenRead("path/to/audio.mp3");
var result = await groqApi.CreateTranscriptionAsync(
    audioStream,
    "audio.mp3",
    GroqModels.WhisperLargeV3Turbo,  // Faster model
    prompt: "Transcribe the following tech conference",
    language: "en"
);
Console.WriteLine(result?["text"]?.ToString());
```

### Audio Translation

```csharp
using var audioStream = File.OpenRead("path/to/french_audio.mp3");
var result = await groqApi.CreateTranslationAsync(
    audioStream,
    "french_audio.mp3",
    GroqModels.WhisperLargeV3,
    prompt: "Translate the following French speech to English"
);
Console.WriteLine(result?["text"]?.ToString());
```

### Tool/Function Calling

```csharp
var weatherTool = new Tool
{
    Type = "function",
    Function = new Function
    {
        Name = "get_weather",
        Description = "Get current weather for a location",
        Parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["location"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The city and state, e.g. San Francisco, CA"
                }
            },
            ["required"] = new JsonArray { "location" }
        },
        ExecuteAsync = async (args) =>
        {
            var jsonArgs = JsonDocument.Parse(args);
            var location = jsonArgs.RootElement.GetProperty("location").GetString();
            // Your weather API call here
            return JsonSerializer.Serialize(new { temperature = 72, condition = "sunny" });
        }
    }
};

var tools = new List<Tool> { weatherTool };
var result = await groqApi.RunConversationWithToolsAsync(
    userPrompt: "What's the weather like in San Francisco?",
    tools: tools,
    model: GroqModels.Llama33_70B,
    systemMessage: "You are a helpful weather assistant.",
    parallelToolCalls: true,
    serviceTier: ServiceTiers.Auto
);
Console.WriteLine(result);
```

### Document Context (RAG)

```csharp
var messages = new JsonArray
{
    new JsonObject
    {
        ["role"] = "user",
        ["content"] = "What are the key points from these documents?"
    }
};

var documents = new JsonArray
{
    new JsonObject
    {
        ["content"] = "Document 1: Our Q3 revenue increased by 25%...",
        ["title"] = "Q3 Financial Report"
    },
    new JsonObject
    {
        ["content"] = "Document 2: Customer satisfaction scores reached 92%...",
        ["title"] = "Customer Survey Results"
    }
};

var result = await groqApi.CreateChatCompletionWithDocumentsAsync(
    messages,
    GroqModels.Llama33_70B,
    documents,
    enableCitations: true
);
Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

### Listing Available Models

```csharp
var modelsResponse = await groqApi.ListModelsAsync();
if (modelsResponse?["data"] is JsonArray models)
{
    foreach (var model in models)
    {
        Console.WriteLine(model?["id"]?.GetValue<string>());
    }
}
```

## 🎛️ Model Reference

> Verified against console.groq.com/docs/models and /deprecations as of 2026-07-12. Deprecated/decommissioned constants are marked `[Obsolete]` in code and are omitted here.

### Chat Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.GptOss120B` | openai/gpt-oss-120b | **Recommended default.** Reasoning + built-in tools, structured outputs (strict). Text-only. |
| `GroqModels.GptOss20B` | openai/gpt-oss-20b | Faster. Structured outputs (strict). |
| `GroqModels.Qwen36_27B` | qwen/qwen3.6-27b | Reasoning + **vision**, 131k context |
| `GroqModels.Allam2_7B` | allam-2-7b | Arabic-focused language model |

### Vision/Multimodal Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.Qwen36_27B` | qwen/qwen3.6-27b | **Recommended vision model.** (`gpt-oss-120b` is text-only.) |

> The former Llama 4 (`Llama4Scout`, `Llama4Maverick`) and Llama 3.2 vision constants are deprecated/decommissioned on GroqCloud. They remain in code as `[Obsolete]` for source compatibility but should not be used.

### Compound Systems
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.Compound` | groq/compound | Multi-tool, web search + code |
| `GroqModels.CompoundMini` | groq/compound-mini | Single tool, 3x faster |

### Audio Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.WhisperLargeV3` | whisper-large-v3 | High accuracy |
| `GroqModels.WhisperLargeV3Turbo` | whisper-large-v3-turbo | Faster |
| `GroqModels.OrpheusEnglish` | canopylabs/orpheus-v1-english | TTS |
| `GroqModels.OrpheusArabic` | canopylabs/orpheus-arabic-saudi | TTS |

### Guard Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.LlamaGuard4` | meta-llama/llama-guard-4-12b | Content safety |
| `GroqModels.PromptGuard22M` | meta-llama/llama-prompt-guard-2-22m | Prompt injection |
| `GroqModels.PromptGuard86M` | meta-llama/llama-prompt-guard-2-86m | Prompt injection |

## 🗣️ TTS Voice Reference

### English Voices (OrpheusVoices, `canopylabs/orpheus-v1-english`)
- `Autumn`, `Diana`, `Hannah`, `Austin`, `Daniel`, `Troy`

### Arabic Voices (`canopylabs/orpheus-arabic-saudi`)
- `Abdullah`, `Fahad`, `Sultan`, `Lulwa`, `Noura`, `Aisha`

### Notes
- Output format is **WAV only**; input is capped at **200 characters**.
- English supports vocal directions in brackets: `[cheerful]`, `[whisper]`, `[dramatic]`, `[excited]`

## 🧰 Request Options & Usage Analytics (v2.1)

Pass strongly-typed parameters with `GroqChatOptions` instead of hand-building every field, and read
token usage / prompt-cache hits / timing from any response with `GroqUsage`:

```csharp
var messages = new JsonArray
{
    new JsonObject { ["role"] = "user", ["content"] = "Explain LPUs in one sentence." }
};

var response = await groqApi.CreateChatCompletionAsync(messages, GroqModels.GptOss120B, new GroqChatOptions
{
    Temperature = 0.2,
    MaxCompletionTokens = 256,
    ServiceTier = ServiceTiers.Flex,
    ReasoningEffort = ReasoningEffort.Low,
    Seed = 42
});

var usage = GroqUsage.FromResponse(response);
if (usage != null)
    Console.WriteLine($"tokens: {usage.TotalTokens} (cached {usage.CachedTokens}, " +
                      $"{usage.CacheHitRatio:P0}), {usage.TotalTime}s, id={usage.RequestId}");
```

Prompt caching is automatic on supported models (e.g. `gpt-oss-120b`); `usage.CachedTokens` shows how
many input tokens were served from cache (billed at a discount).

## 🎙️ Transcription from a URL (v2.1)

```csharp
var result = await groqApi.CreateTranscriptionFromUrlAsync(
    "https://example.com/audio.mp3", GroqModels.WhisperLargeV3Turbo);
```

## 🧠 Compound: enable specific tools & inspect what ran (v2.1)

```csharp
var response = await groqApi.CreateCompoundCompletionAsync(
    messages,
    enabledTools: new[] { "web_search", "code_interpreter" });

var executed = GroqApiClient.GetExecutedTools(response); // which built-in tools the system ran
```

## 🧰 Built-in server-side tools (v2.3)

Typed builders and identifiers so you can attach Groq's built-in, server-executed tools without hand-writing JSON.

**gpt-oss models** take built-in tools in the `tools` array:

```csharp
var response = await groqApi.CreateChatCompletionWithBuiltInToolsAsync(
    messages,
    GroqModels.GptOss120B,
    new[] { GroqBuiltInTools.BrowserSearch() },          // and/or GroqBuiltInTools.CodeInterpreter()
    new GroqChatOptions { ToolChoice = "required" });    // optional
```

**Compound systems** enable tools by name — pass the typed constants instead of magic strings:

```csharp
var response = await groqApi.CreateCompoundCompletionAsync(
    messages,
    enabledTools: new[] { GroqBuiltInTools.Compound.WebSearch, GroqBuiltInTools.Compound.VisitWebsite });
```

Read what actually ran — typed, works for both compound and gpt-oss:

```csharp
foreach (var tool in GroqExecutedTool.FromResponse(response))
    Console.WriteLine($"{tool.Type ?? tool.Name}: {tool.Output}");   // .Raw for tool-specific fields
```

## 🧪 Responses API (beta, v2.3)

OpenAI-compatible Responses API (`POST /openai/v1/responses`). **Beta** surface — may change with the upstream API.

```csharp
// Plain string input
var resp = await groqApi.CreateResponseAsync(
    GroqModels.GptOss120B,
    "Explain quicksort in one sentence.",
    new GroqResponseOptions { ReasoningEffort = "low", MaxOutputTokens = 200 });

string? answer = GroqApiClient.GetResponseOutputText(resp);   // top-level output_text (falls back to output[])
GroqUsage usage = GroqUsage.FromResponse(resp)!;              // maps input_tokens/output_tokens too
```

You can also pass a message array as `input`, and set `Instructions`, `Tools`, `ToolChoice`, `Text` (JSON-schema output), `Temperature`, `Top_p`, and `Metadata` via `GroqResponseOptions`.

> **Stateful conversations are not supported on Groq** (`previous_response_id`/`store` are unavailable) — keep the history yourself and pass it in `input` on every call.

## 🌱 Optional Prompt Compression (v2.1)

A "greening"/cost feature: reduce a prompt's token footprint before sending. This is **opt-in lossy
token reduction** (the model reads the transformed text — there is no reversible decompress), so weigh
the quality/cost trade-off. When no compressor is supplied, prompts are sent unchanged.

```csharp
// Zero-cost, near-lossless: collapses redundant whitespace, no network call.
var provider = new GroqLlmProvider(apiKey, GroqModels.GptOss120B, new WhitespaceCompressor());

// Optional LLM-based compression for LARGE prompts (adds a call; net-negative on small prompts,
// so it no-ops below MinCharsToCompress):
var llmCompressor = new LlmSummarizingCompressor(new GroqApiClient(apiKey), GroqModels.GptOss20B);
var provider2 = new GroqLlmProvider(apiKey, GroqModels.GptOss120B, llmCompressor);
```

Implement `IPromptCompressor` for a custom strategy. Measure the effect with `GroqUsage.PromptTokens`
before/after rather than assuming savings.

### Ecosystem note
If you're building a **coding agent** on top of this library, most of your prompt tokens are usually
*source code context*, not prose. Tools like [jcodemunch-mcp](https://github.com/jgravelle) can rank and
pack the relevant code under a token budget **upstream** — before the prompt reaches Groq — which
typically saves far more than compressing the assembled prompt. That is complementary to, not a
replacement for, the `IPromptCompressor` seam above.

## 📦 Files & Batch Processing (v2.2)

Run large jobs asynchronously at a 50% discount. Build a JSONL input with `BatchRequestBuilder`,
upload it via the Files API, create a batch, poll until terminal, then download and parse the output.

> ⚠️ The Files and Batch APIs require an eligible Groq plan. On unsupported plans the API returns
> `403 not_available_for_plan`.

```csharp
// 1) Build JSONL input (one line per request; custom_id correlates results)
var builder = new BatchRequestBuilder(BatchEndpoints.ChatCompletions)
    .Add("req-1", new JsonObject {
        ["model"] = GroqModels.GptOss20B,
        ["messages"] = new JsonArray { new JsonObject { ["role"]="user", ["content"]="Summarize X" } }
    })
    .Add("req-2", new JsonObject {
        ["model"] = GroqModels.GptOss20B,
        ["messages"] = new JsonArray { new JsonObject { ["role"]="user", ["content"]="Summarize Y" } }
    });

// 2) Upload + create the batch
var file  = await groqApi.UploadFileAsync(builder.BuildStream(), "batch_input.jsonl");
var batch = GroqBatch.FromResponse(
    await groqApi.CreateBatchAsync(file!["id"]!.GetValue<string>(), BatchEndpoints.ChatCompletions, "24h"));

// 3) Poll until terminal
while (!batch!.IsTerminal)
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    batch = GroqBatch.FromResponse(await groqApi.GetBatchAsync(batch.Id!));
}

// 4) Download + parse results
if (batch.IsCompleted)
{
    var bytes   = await groqApi.GetFileContentAsync(batch.OutputFileId!);
    var results = BatchJsonl.ParseOutput(bytes);   // List<BatchOutputLine>
    foreach (var r in results)
        Console.WriteLine($"{r.CustomId}: {(r.IsSuccess ? r.Body?["choices"]?[0]?["message"]?["content"] : r.Error)}");
}
```

Files API methods are also available directly: `UploadFileAsync`, `ListFilesAsync`, `GetFileAsync`,
`GetFileContentAsync` / `GetFileContentStreamAsync`, `DeleteFileAsync`.

## ⚙️ Advanced Configuration

### Dependency Injection (ASP.NET Core / Generic Host)

Register `IGroqApiClient` with a factory-managed, pooled `HttpClient` (avoids socket exhaustion) via `AddGroqApiClient`:

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddGroqApiClient(builder.Configuration["Groq:ApiKey"]!);

// Optional: customize the managed HttpClient (timeout, proxy, etc.)
builder.Services.AddGroqApiClient(apiKey, http => http.Timeout = TimeSpan.FromSeconds(60));
```

Then inject `IGroqApiClient` anywhere:

```csharp
public class MyService(IGroqApiClient groq)
{
    public Task<JsonObject?> AskAsync(JsonArray messages) =>
        groq.CreateChatCompletionAsync(messages, GroqModels.Llama33_70B);
}
```

`AddGroqApiClient` returns the `IHttpClientBuilder`, so you can chain resilience/retry handlers or set the handler lifetime.

### Custom Headers (e.g., Compound versioning)

```csharp
groqApi.SetCustomHeader("Groq-Model-Version", "latest");
```

### Service Tiers

```csharp
// Available tiers: auto, on_demand, flex, performance
var request = new JsonObject
{
    ["model"] = GroqModels.GptOss120B,
    ["service_tier"] = ServiceTiers.Performance,
    // ...
};
```

## 🛡️ Error Handling

Non-2xx responses throw a `GroqApiException` carrying the parsed error envelope (`ErrorCode`, `ErrorType`, `ResponseBody`) and the HTTP `StatusCode`. It **derives from `HttpRequestException`**, so existing `catch (HttpRequestException)` code keeps working unchanged — you only opt into the richer types where you want them:

```csharp
try
{
    var result = await groqApi.CreateChatCompletionAsync(request);
}
catch (GroqRateLimitException e)          // HTTP 429
{
    Console.WriteLine($"Rate limited; retry after {e.RetryAfter}");
}
catch (GroqAuthenticationException e)     // HTTP 401
{
    Console.WriteLine($"Bad API key: {e.ErrorCode}");
}
catch (GroqApiException e)                // any other non-2xx (400/403/404/5xx…)
{
    Console.WriteLine($"Groq error {e.StatusCode} ({e.ErrorType}/{e.ErrorCode}): {e.Message}");
}
catch (HttpRequestException e)            // still catches everything above, for legacy code
{
    Console.WriteLine($"API request failed: {e.Message}");
}
```

Typed subtypes: `GroqBadRequestException` (400), `GroqAuthenticationException` (401), `GroqPermissionException` (403 — e.g. an endpoint not on your plan), `GroqNotFoundException` (404), `GroqRateLimitException` (429, with `RetryAfter`), `GroqServerException` (5xx).

### Retries & timeouts

Resilience is layered on the `HttpClient` rather than baked into the client. With the DI registration you can add the standard resilience handler (retries 408/429/5xx + timeout + circuit breaker):

```csharp
builder.Services.AddGroqApiClient(cfg["Groq:ApiKey"]!)
    .AddStandardResilienceHandler();   // requires the Microsoft.Extensions.Http.Resilience package
```

> Note: retrying is safe for idempotent failures (429/5xx/timeouts). Streaming calls can only be retried before the first chunk is consumed — a partially-read SSE stream can't be replayed.

## 🔄 Migration from v1.x

v2.0 is backwards compatible. Existing code will continue to work. New features are additive.

**Notable changes:**
- `max_tokens` deprecated in favor of `max_completion_tokens`
- Added `GroqModels`, `OrpheusVoices`, `ServiceTiers`, `ReasoningEffort`, `ReasoningFormat` static classes for convenience

### v2.3 (unreleased)
- **Responses API (beta)** — `CreateResponseAsync(model, input, GroqResponseOptions?)` for the OpenAI-compatible `POST /openai/v1/responses`, with `GetResponseOutputText` to read the answer and typed `GroqResponseOptions`. `GroqUsage.FromResponse` now also maps the Responses `input_tokens`/`output_tokens` shape. Stateful conversations aren't supported on Groq — pass full history each call.
- **Built-in server-side tools helpers** — `GroqBuiltInTools` builders (`BrowserSearch()`, `CodeInterpreter()`) and identifier constants (`GroqBuiltInTools.Compound.*`) for adding Groq's server-side tools without raw JSON, a `CreateChatCompletionWithBuiltInToolsAsync` convenience extension, and a typed `GroqExecutedTool.FromResponse` for inspecting `executed_tools` on both Compound and gpt-oss runs.
- **Typed API errors** — non-2xx responses now throw `GroqApiException` (and status-specific subtypes: `GroqRateLimitException`, `GroqAuthenticationException`, `GroqPermissionException`, `GroqBadRequestException`, `GroqNotFoundException`, `GroqServerException`) exposing `StatusCode`, parsed `ErrorCode`/`ErrorType`, and `ResponseBody`. All derive from `HttpRequestException`, so existing catch blocks are unaffected. Streaming errors now include the response body too.

### v2.2 (2026-07)
- **Files API** — `UploadFileAsync`, `ListFilesAsync`, `GetFileAsync`, `GetFileContentAsync`/`GetFileContentStreamAsync`, `DeleteFileAsync`.
- **Batch API** — `CreateBatchAsync`/`GetBatchAsync`/`ListBatchesAsync`/`CancelBatchAsync`, plus `BatchRequestBuilder`, `BatchJsonl.ParseOutput`, and the typed `GroqBatch`. (Files/Batch require an eligible Groq plan.)
- **Streaming with options + usage** — `CreateChatCompletionStreamAsync(messages, model, options)`; set `StreamIncludeUsage` to receive usage in the final chunk.
- Added `GroqModels.Allam2_7B`.

### v2.1.1 (2026-07)
- **Fix:** audio transcription/translation now format the `temperature` value with `InvariantCulture`, so requests no longer send `0,5` (and get rejected) on comma-decimal locales.

### v2.1 (2026-07)
- **Model catalog refreshed** to Groq's current lineup. Decommissioned/deprecated IDs (Kimi K2, Llama 4 Scout/Maverick, Qwen3-32B, Llama 3.2 vision) are now marked `[Obsolete]`.
- **Default vision model is now `qwen/qwen3.6-27b`** (the Llama 4 vision models are deprecated; `gpt-oss-120b` is text-only).
- **Corrected Orpheus TTS voices** — the previous voice constants were PlayAI names and did not work with Orpheus. Use `autumn/diana/hannah/austin/daniel/troy` (English) and `abdullah/fahad/sultan/lulwa/noura/aisha` (Arabic). Orpheus output is WAV-only.

## 🛠️ Contributing

We welcome contributions! Please:
1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 🚀 Releasing (maintainers)

Publishing to NuGet is automated via **Trusted Publishing** (OIDC — no stored API key). The
`.github/workflows/publish.yml` workflow runs when a **GitHub Release is published**, or manually via
the Actions tab (`workflow_dispatch`).

> ⚠️ The published version comes from `<Version>` in `GroqApiLibrary.csproj`, **not** from the release
> tag name. Always bump the version *before* creating the release — otherwise the workflow re-packs the
> existing version and `--skip-duplicate` makes the push a harmless no-op (nothing gets published).

**To cut a release:**
1. Bump `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in `GroqApiLibrary.csproj` (e.g. `2.1.0` → `2.2.0`) and commit to `master`.
2. Create a GitHub Release with a tag that matches the version (e.g. `v2.2.0`):
   ```bash
   gh release create v2.2.0 --target master --title "v2.2.0" --notes "…release notes…"
   ```
3. The workflow packs and pushes automatically. Watch it with `gh run watch` and confirm the package
   appears at `nuget.org/packages/GroqApiLibrary`.

**Does not** trigger a publish: a plain `git push`, pushing a tag on its own, or a *draft* release —
only a **published** Release (or a manual run) does.

## 📄 License

This library is licensed under the MIT License.
Mention J. Gravelle if you use this code. He's sort of full of himself.

## 🙏 Acknowledgements

- Special thanks to the Groq team for their incredible AI models and API
- Shoutout to all contributors who have helped improve this library

---

Happy coding! 🚀
