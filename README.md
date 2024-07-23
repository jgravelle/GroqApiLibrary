# Groq API C# Client Library

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously using .NET 8.

## Installation

To use this library in your .NET 8 project:

1. Copy the `GroqApiClient.cs` file into your project.
2. Ensure your project targets .NET 8 or later.

## Usage

1. Create an instance of the `GroqApiClient` class, providing your API key.
2. Create a `JsonObject` with your request parameters as documented in the Groq API documentation.
3. Use the client to send requests and receive responses.

## Examples

### Standard Chat Completion

```csharp
using GroqApiLibrary;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main(string[] args)
    {
        string apiKey = "your_api_key_here";
        var groqApi = new GroqApiClient(apiKey);

        var request = new JsonObject
        {
            ["model"] = "mixtral-8x7b-32768", // Other models: llama2-70b-chat, gemma-7b-it, llama3-70b-8192, llama3-8b-8192
            ["temperature"] = 0.5,
            ["max_tokens"] = 100,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
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
                    ["content"] = "Write a haiku about coding."
                }
            }
        };

        var result = await groqApi.CreateChatCompletionAsync(request);
        var response = result?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No response found";
        Console.WriteLine(response);
    }
}
```

### Streaming Chat Completion

```csharp
using GroqApiLibrary;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main()
    {
        string apiKey = "your_api_key_here";
        var groqApi = new GroqApiClient(apiKey);

        var request = new JsonObject
        {
            ["model"] = "mixtral-8x7b-32768",
            ["temperature"] = 0.5,
            ["max_tokens"] = 100,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
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
                    ["content"] = "Explain quantum computing in simple terms."
                }
            }
        };

        await foreach (var chunk in groqApi.CreateChatCompletionStreamAsync(request))
        {
            var delta = chunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty;
            Console.Write(delta);
        }
    }
}
```

## Features

- Built for .NET 8, taking advantage of the latest C# features.
- Uses `System.Text.Json` for efficient JSON handling.
- Supports both synchronous and streaming API calls.
- Implements `IDisposable` for proper resource management.
- Nullable aware, helping to prevent null reference exceptions.

## Latest Updates

- Upgraded to .NET 8 compatibility.
- Removed dependency on Newtonsoft.Json, now using `System.Text.Json`.
- Improved null handling with nullable reference types.
- Simplified API calls using `HttpClient.PostAsJsonAsync`.
- Enhanced streaming support with `IAsyncEnumerable`.

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## License

This library is licensed under the MIT License. See the LICENSE file for more information.

## Special Thanks

- Marcus Cazzola for significant contributions to the library's development.
- Joaquin Grech for advocating the transition from Newtonsoft.Json to System.Text.Json.
