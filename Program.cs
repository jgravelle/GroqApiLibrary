using GroqApiLibrary;
using Newtonsoft.Json.Linq;

class Program
{
    static async Task Main()
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
