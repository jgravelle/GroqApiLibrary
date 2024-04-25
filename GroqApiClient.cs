using System.Text;
using Newtonsoft.Json.Linq;

namespace GroqApiLibrary
{
    public class GroqApiClient : IGroqApiClient
    {
        private readonly HttpClient client = new();

        public GroqApiClient(string apiKey)
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        //The available request parameters are listed in the Groq API documentation at https://console.groq.com/docs/text-chat
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
    }
}