using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace GroqApiLibrary;

public class GroqApiClient : IGroqApiClient
{
    private readonly HttpClient client = new();

    public GroqApiClient(string apiKey)
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<JObject> CreateChatCompletionAsync(JObject request)
    {
        request.Add("response_format", new JObject(new JProperty("type", "json_object")));
        StringContent httpContent = new StringContent(request.ToString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        string responseString = await response.Content.ReadAsStringAsync();
        JObject responseJson = JObject.Parse(responseString);

        return responseJson;
    }

    public async IAsyncEnumerable<JObject> CreateChatCompletionStreamAsync(JObject request)
    {
        request.Add("stream", true);
        StringContent httpContent = new StringContent(request.ToString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data != "[DONE]")
                {
                    yield return JObject.Parse(data);
                }
            }
        }
    }
}