# Groq API C# Client Library

Welcome to the Groq API C# Client Library! This powerful and flexible library provides a seamless interface to interact with the cutting-edge Groq AI API. Designed for .NET 8 and above, our library offers a range of features to enhance your AI-powered applications.

## 🌟 Features

- 💬 **Chat Completions**: Engage in dynamic conversations with AI models
- 🔊 **Audio Transcription**: Convert speech to text with high accuracy
- 🌐 **Audio Translation**: Translate audio content across languages
- 🛠️ **Tool Usage**: Extend AI capabilities with custom tools
- 🌊 **Streaming Support**: Real-time responses for interactive applications
- 📋 **Model Listing**: Retrieve available AI models
- 👁️ **Vision Analysis**: Process and analyze images with multimodal models

## 📦 Installation

To use this library in your .NET 8+ project:

1. Clone this repository or download the `GroqApiClient.cs` file.
2. Add the file to your project.
3. Ensure your project targets .NET 8 or later.

## 🚀 Quick Start

Here's a simple example to get you started:

```csharp
using GroqApiLibrary;
using System.Text.Json.Nodes;

var apiKey = "your_api_key_here";
var groqApi = new GroqApiClient(apiKey);

var request = new JsonObject
{
    ["model"] = "mixtral-8x7b-32768",
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
    ["model"] = "mixtral-8x7b-32768",
    ["temperature"] = 0.7,
    ["max_tokens"] = 150,
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
    ["model"] = "mixtral-8x7b-32768",
    ["messages"] = new JsonArray
    {
        new JsonObject
        {
            ["role"] = "user",
            ["content"] = "Explain the concept of quantum entanglement."
        }
    }
};

await foreach (var chunk in groqApi.CreateChatCompletionStreamAsync(request))
{
    var delta = chunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty;
    Console.Write(delta);
}
```

### Vision Analysis

#### Basic Image Analysis

```csharp
var result = await groqApi.CreateVisionCompletionWithImageUrlAsync(
    "https://example.com/image.jpg",
    "What's in this image?",
    "llama-3.2-90b-vision-preview"
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

#### Local Image Analysis

```csharp
var result = await groqApi.CreateVisionCompletionWithBase64ImageAsync(
    "path/to/local/image.jpg",
    "Describe this image",
    "llama-3.2-90b-vision-preview"
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

#### Vision with JSON Mode

```csharp
var result = await groqApi.CreateVisionCompletionWithJsonModeAsync(
    "https://example.com/image.jpg",
    "List all objects in this image in JSON format",
    "llama-3.2-90b-vision-preview"
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

#### Vision with Tools

```csharp
var weatherTool = new Tool
{
    Type = "function",
    Function = new Function
    {
        Name = "get_weather",
        Description = "Get weather information for a location",
        Parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["location"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The city and state"
                }
            },
            ["required"] = new JsonArray { "location" }
        }
    }
};

var tools = new List<Tool> { weatherTool };
var result = await groqApi.CreateVisionCompletionWithToolsAsync(
    "https://example.com/cityscape.jpg",
    "What's the weather like in this city?",
    tools,
    "llama-3.2-90b-vision-preview"
);

Console.WriteLine(result?["choices"]?[0]?["message"]?["content"]?.ToString());
```

#### Complex Vision Analysis with VisionAgent

For more complex vision tasks, use the VisionAgent:

```csharp
var llmProvider = new GroqLlmProvider(apiKey, "llama-3.2-90b-vision-preview");
var visionAgent = new VisionAgent(llmProvider, "llama-3.2-90b-vision-preview", debug: true);

var result = await visionAgent.ProcessRequestAsync(
    "Analyze this image and provide historical context: https://example.com/image.jpg"
);

Console.WriteLine(result);
```

### Audio Transcription

```csharp
using (var audioStream = File.OpenRead("path/to/your/audio.mp3"))
{
    var result = await groqApi.CreateTranscriptionAsync(
        audioStream,
        "audio.mp3",
        "whisper-large-v3",
        prompt: "Transcribe the following tech conference",
        language: "en"
    );
    Console.WriteLine(result?["text"]?.ToString());
}
```

### Audio Translation

```csharp
using (var audioStream = File.OpenRead("path/to/your/french_audio.mp3"))
{
    var result = await groqApi.CreateTranslationAsync(
        audioStream,
        "french_audio.mp3",
        "whisper-large-v3",
        prompt: "Translate the following French speech to English"
    );
    Console.WriteLine(result?["text"]?.ToString());
}
```

### Tool Usage

Enhance your AI's capabilities by integrating custom tools. Here's an example using a simple math calculator:

```csharp
var calculateTool = new Tool
{
    Type = "function",
    Function = new Function
    {
        Name = "calculate",
        Description = "Perform a mathematical calculation",
        Parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["expression"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The mathematical expression to evaluate"
                }
            },
            ["required"] = new JsonArray { "expression" }
        },
        ExecuteAsync = async (args) =>
        {
            var jsonArgs = JsonDocument.Parse(args);
            var expression = jsonArgs.RootElement.GetProperty("expression").GetString();
            try
            {
                var result = new System.Data.DataTable().Compute(expression, null);
                return JsonSerializer.Serialize(new { result = result.ToString() });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Error calculating: {ex.Message}" });
            }
        }
    }
};

var tools = new List<Tool> { calculateTool };
var model = "mixtral-8x7b-32768";
var systemMessage = "You are an assistant that can perform calculations.";
var userPrompt = "What is the square root of 144 plus 50?";

var result = await groqApi.RunConversationWithToolsAsync(userPrompt, tools, model, systemMessage);
Console.WriteLine(result);
```

### Listing Available Models

To retrieve a list of available AI models:

```csharp
var modelsResponse = await groqApi.ListModelsAsync();
if (modelsResponse != null && modelsResponse.TryGetPropertyValue("data", out var dataNode))
{
    var models = dataNode.AsArray();
    foreach (var model in models)
    {
        Console.WriteLine(model?["id"]?.GetValue<string>());
    }
}
```

## 🎛️ Advanced Configuration

### Supported Models

Our library supports a wide range of Groq models, including:

- mixtral-8x7b-32768
- llama3-70b-8192
- llama3-8b-8192
- gemma-7b-it

### Supported Vision Models

The library supports the following vision models:
- llama-3.2-90b-vision-preview: High-capacity vision model
- llama-3.2-11b-vision-preview: Efficient vision model

### Error Handling

The library uses exception handling to manage errors. Always wrap your API calls in try-catch blocks for robust error management:

```csharp
try
{
    var result = await groqApi.CreateChatCompletionAsync(request);
    // Process result
}
catch (HttpRequestException e)
{
    Console.WriteLine($"API request failed: {e.Message}");
}
catch (JsonException e)
{
    Console.WriteLine($"Failed to parse API response: {e.Message}");
}
```

## 🛠️ Contributing

We welcome contributions to the Groq API C# Client Library! If you have suggestions for improvements or bug fixes, please:

1. Fork the repository
2. Create a new branch for your feature
3. Commit your changes
4. Push to the branch
5. Create a new Pull Request

## 📄 License

This library is licensed under the MIT License.
Mention J. Gravelle if you use this code. He's sort of full of himself.

## 🙏 Acknowledgements

- Special thanks to the Groq team for their incredible AI models and API.
- Shoutout to all contributors who have helped improve this library.

---

We hope you enjoy using the Groq API C# Client Library! If you have any questions or need further assistance, please open an issue in this repository. Happy coding! 🚀