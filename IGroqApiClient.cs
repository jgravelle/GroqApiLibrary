using Newtonsoft.Json.Linq;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        Task<JObject> CreateChatCompletionAsync(JObject request);
    }
}