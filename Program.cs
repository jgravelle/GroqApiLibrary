using Newtonsoft.Json.Linq;

class Program
{
    static void Main(string[] args)
    {
        string key = "xxxxxxxxxxx";
        var groqApi = new GroqApiLibrary.GroqApiClient(key);

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

        JObject result = groqApi.CreateChatCompletionAsync(request).Result;

        string? response = result?["choices"]?[0]?["message"]?["content"]?.ToString();
        Console.WriteLine(response);
        Console.ReadLine();
    }
}
