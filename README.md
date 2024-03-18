# Groq API Client Library

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously through the `IGroqApiClient` interface.

## Installation

1. To use this library, you'll need to install the following NuGet packages:

- Newtonsoft.Json
``` dotnet add package Newtonsoft.Json ```

Copy the GroqApiClient.cs file and the IGroqApiClient interface file into your project.

## Usage
1. Implement the IGroqApiClient interface in your application. An example implementation, GroqApiClient, is provided.
2. Create an instance of the GroqApiClient class (or any class that implements IGroqApiClient), providing your API key.
3. Create a Newtonsoft JSON object (JObject) with your request. The available parameters are listed in the Groq API documentation.
4. Receive the response, which is also a JObject, and extract the response information accordingly.


## Example
using GroqApiLibrary;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string apiKey = "xxxxxxxxx";
        IGroqApiClient groqApi = new GroqApiClient(apiKey);

        JObject request = new()
        {
            ["model"] = "mixtral-8x7b-32768", // LLaMA2-70b-chat or Gemma-7b-it also supported
            ["temperature"] = 0.5,
            ["max_tokens"] = 100,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a chatbot capable of anything and everything."
                },
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = "Write a poem about GitHub."
                }
            }
        };

        JObject result = await groqApi.CreateChatCompletionAsync(request);

        string response = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No response found";
        Console.WriteLine(response);
        Console.ReadLine();
    }
}

## Contributing
Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## License
This library is licensed under the MIT License. See the LICENSE file for more information.

## Special Thanks
Marcus Cazzola , who did most of the heavy lifting.
