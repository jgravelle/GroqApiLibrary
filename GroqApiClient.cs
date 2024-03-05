using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GroqApiLibrary
{
    public class GroqApiClient
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _client = new(); // Ensure non-null

        public GroqApiClient(string apiKey, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string?> SendRequestAndGetResponseAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            }

            var requestContent = new
            {
                messages = new[]
                {
                    new { role = "user", content }
                },
                model = _model,
            };

            var httpContent = new StringContent(JsonConvert.SerializeObject(requestContent), Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Received response body: {responseBody}");

                dynamic? result = JsonConvert.DeserializeObject(responseBody);
                return result?.choices[0]?.message?.content?.ToString();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Exception Caught! Message :{e.Message}");
                return null;
            }
        }
    }
}