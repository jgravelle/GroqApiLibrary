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

### Chat Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.Llama33_70B` | llama-3.3-70b-versatile | Production, 131k context |
| `GroqModels.Llama31_8B` | llama-3.1-8b-instant | Fast, 131k context |
| `GroqModels.GptOss20B` | openai/gpt-oss-20b | Structured outputs (strict) |
| `GroqModels.GptOss120B` | openai/gpt-oss-120b | Structured outputs (strict) |
| `GroqModels.Qwen3_32B` | qwen/qwen3-32b | Reasoning support |
| `GroqModels.KimiK2` | moonshotai/kimi-k2-instruct-0905 | 262k context |

### Vision/Multimodal Models
| Constant | Model ID | Notes |
|----------|----------|-------|
| `GroqModels.Llama4Scout` | meta-llama/llama-4-scout-17b-16e-instruct | Efficient, 131k context |
| `GroqModels.Llama4Maverick` | meta-llama/llama-4-maverick-17b-128e-instruct | High capacity |

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

### English Voices (OrpheusVoices)
- `Tara`, `Leah`, `Jess`, `Mia`, `Zoe` (female)
- `Leo`, `Dan`, `Zac` (male)

### Arabic Voices
- `Abdullah` (male), `Amira` (female)

### Vocal Directions
Add emotion/style in brackets: `[cheerful]`, `[sad]`, `[serious]`, `[excited]`, `[whisper]`

## ⚙️ Advanced Configuration

### Custom Headers (e.g., Compound versioning)

```csharp
groqApi.SetCustomHeader("Groq-Model-Version", "latest");
```

### Service Tiers

```csharp
// Available tiers: auto, on_demand, flex, performance
var request = new JsonObject
{
    ["model"] = GroqModels.Llama33_70B,
    ["service_tier"] = ServiceTiers.Performance,
    // ...
};
```

## 🛡️ Error Handling

```csharp
try
{
    var result = await groqApi.CreateChatCompletionAsync(request);
}
catch (HttpRequestException e)
{
    Console.WriteLine($"API request failed: {e.Message}");
}
catch (JsonException e)
{
    Console.WriteLine($"Failed to parse response: {e.Message}");
}
```

## 🔄 Migration from v1.x

v2.0 is backwards compatible. Existing code will continue to work. New features are additive.

**Notable changes:**
- Default vision model changed from `llama-3.2-90b-vision-preview` to `meta-llama/llama-4-scout-17b-16e-instruct`
- `max_tokens` deprecated in favor of `max_completion_tokens`
- Added `GroqModels`, `OrpheusVoices`, `ServiceTiers`, `ReasoningEffort`, `ReasoningFormat` static classes for convenience

## 🛠️ Contributing

We welcome contributions! Please:
1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 License

This library is licensed under the MIT License.
Mention J. Gravelle if you use this code. He's sort of full of himself.

## 🙏 Acknowledgements

- Special thanks to the Groq team for their incredible AI models and API
- Shoutout to all contributors who have helped improve this library

---

Happy coding! 🚀
