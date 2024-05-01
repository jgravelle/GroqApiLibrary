using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GroqApiLibrary;

public class GroqApiClient : IGroqApiClient
{
    private readonly HttpClient client = new();

    public GroqApiClient(string apiKey)
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<JsonObject?> CreateChatCompletionAsync(JsonObject request)
    {
        // Commented out until stabilized on Groq
        // the API is still not accepting the request payload in its documented format, even after following the JSON mode instructions.
        // request.Add("response_format", new JsonObject(new KeyValuePair<string, JsonNode?>("type", "json_object")));

        StringContent httpContent = new(request.ToJsonString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        string responseString = await response.Content.ReadAsStringAsync();
        JsonObject? responseJson = JsonSerializer.Deserialize<JsonObject>(responseString);

        return responseJson;
    }

    public async IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request)
    {
        request.Add("stream", true);

        StringContent httpContent = new(request.ToJsonString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            String? line = await reader.ReadLineAsync();
            if (line is not null && line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data != "[DONE]")
                {
                    yield return JsonSerializer.Deserialize<JsonObject>(data);
                }
            }
        }
    }
}