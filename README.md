# Groq API Client Library

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously.

## Installation

1. To use this library, you'll need to install the following NuGet packages:

- Newtonsoft.Json

```bash
dotnet add package Newtonsoft.Json
```
2. Copy the `GroqApiClient.cs` file into your project.

## Usage
1. Create an instance of the `GroqApiClient` class, providing your API key.
2. Create a Newtonsoft JSON object called `JObject` with your request. The available parameters are listed in the [Groq API documentation](https://console.groq.com/docs/text-chat).
3. Receive the response, which is a `JObject`, and extract the response information.

## Example
```cs
string key = "xxxxxxxxxxx";
GroqApiLibrary.GroqApiClient groqApi = new GroqApiLibrary.GroqApiClient(key);

JObject request = new JObject
{
    ["model"] = "mixtral-8x7b-32768",
    ["messages"] = new JArray
    {
        new JObject
        {
            ["role"] = "system",
            ["content"] = "You are a chatbot that holds every answer to every question"
        },
        new JObject
        {
            ["role"] = "user",
            ["content"] = "What is the meaning of life?"
        }
    }
};

JObject result = await groqApi.CreateChatCompletionAsync(request);

string? response = result?["choices"]?[0]?["message"]?["content"]?.ToString();
Console.WriteLine(response);
```

## Contributing
Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## License
This project is licensed under the MIT License.
