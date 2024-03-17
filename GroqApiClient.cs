using System.Text;
using Newtonsoft.Json.Linq;

namespace GroqApiLibrary
{
    public class GroqApiClient
    {
        private HttpClient client = new HttpClient(); // Ensure non-null

        public GroqApiClient(string apiKey)
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<JObject> CreateChatCompletionAsync(JObject request)
        {
            StringContent httpContent = new StringContent(request.ToString(), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();
            string responseString = await response.Content.ReadAsStringAsync();

            JObject responseJson = JObject.Parse(responseString);
            return responseJson;
        }
    }
}