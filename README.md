# Groq API Client Library

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously.

## Installation

To use this library, you'll need to install the following NuGet packages:

- Newtonsoft.Json

```bash
dotnet add package Newtonsoft.Json

markdown


Copy code
**# Groq API Client Library**

This library provides a simple interface to interact with the Groq AI API. It allows you to send requests to the API and receive responses asynchronously.

**## Installation**

To use this library, you'll need to install the following NuGet packages:

- Newtonsoft.Json

```bash
dotnet add package Newtonsoft.Json


**Usage**
1.  Create an instance of the GroqApiClient class, providing your API key and the desired model:

string apiKey = "your_api_key_here";
string model = "mixtral-8x7b-32768"; // or any other supported model
var client = new GroqApiClient(apiKey, model);

2.  Send a request to the API by calling the SendRequestAndGetResponseAsync method with the desired content:

string content = "Your input text here";
string? response = await client.SendRequestAndGetResponseAsync(content);
if (response != null)
{
    Console.WriteLine(response);
}

**Example**
Here's an example of how to use the LlmCommunicationService class, which wraps the GroqApiClient:

var service = new LlmCommunicationService();
string input = "What is the capital of France?";
string response = await service.GetResponseAsync(input);
Console.WriteLine(response);

This will send the input prompt to the Groq API and print the response to the console.

**Contributing**
Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

**License**
This project is licensed under the MIT License.
