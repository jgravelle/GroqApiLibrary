# Groq API C# Client Library

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously through the `IGroqApiClient` interface.

## Installation

To use this library, you'll need to copy the `GroqApiClient.cs` file and the `IGroqApiClient` interface file into your project.

## Usage

1. Implement the `IGroqApiClient` interface in your application. An example implementation, `GroqApiClient`, is provided.
2. Create an instance of the `GroqApiClient` class (or any class that implements `IGroqApiClient`), providing your API key.
3. Create a `JsonObject` with your request. The available parameters are listed in the Groq API documentation.
4. Receive the response, which is also a `JsonObject`, and extract the response information accordingly.

## Examples

### Standard Chat Completion

```csharp
using GroqApiLibrary;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string apiKey = "xxxxxxxxx";
        IGroqApiClient groqApi = new GroqApiClient(apiKey);

        JsonObject request = new()
        {
            ["model"] = "mixtral-8x7b-32768", // llama2-70b-chat | gemma-7b-it | llama3-70b-8192| llama3-8b-8192 also supported
            ["temperature"] = 0.5,
            ["max_tokens"] = 100,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a chatbot capable of anything and everything."
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = "Write a poem about GitHub."
                }
            }
        };

        JsonObject? result = await groqApi.CreateChatCompletionAsync(request);
        string response = result?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No response found";
        Console.WriteLine(response);
        Console.ReadLine();
    }
}
```

### Streaming Chat Completion

```csharp
using GroqApiLibrary;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

class Program_streaming
{
    static async Task Main()
    {
        string apiKey = "xxxxxxxxx";
        IGroqApiClient groqApi = new GroqApiClient(apiKey);

        JsonObject request = new()
        {
            ["model"] = "mixtral-8x7b-32768", // LLaMA2-70b-chat or Gemma-7b-it also supported
            ["temperature"] = 0.5,
            ["max_tokens"] = 100,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a chatbot capable of anything and everything."
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = "Write a poem about GitHub."
                }
            }
        };

        await foreach (JsonObject? chunk in groqApi.CreateChatCompletionStreamAsync(request))
        {
            string delta = chunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty;
            Console.Write(delta);
        }

        Console.WriteLine();
        Console.ReadLine();
    }
}
```

## Latest Updates

- The library now uses the native `System.Text.Json` instead of `Newtonsoft.Json` for JSON serialization and deserialization.
- The `JsonObject` and `JsonArray` types from `System.Text.Json.Nodes` are used instead of `JObject` and `JArray` from `Newtonsoft.Json.Linq`.
- The `CreateChatCompletionAsync` and `CreateChatCompletionStreamAsync` methods now return nullable `JsonObject` and `IAsyncEnumerable<JsonObject?>` respectively.
- The code has been updated to handle nullable types and use null-conditional operators to avoid potential null reference exceptions.

The modifications to switch from `Newtonsoft.Json` to `System.Text.Json` should not impact existing client applications that are using this library. The API and usage of the library remain the same, and the only change is the underlying JSON library used. Existing client applications should continue to work without any modifications.

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## License

This library is licensed under the MIT License. See the LICENSE file for more information.

## Special Thanks

Marcus Cazzola, who did some of the heavy lifting.  And Joaquin Grech for coaxing me to kick NewtonSoft to the curb.