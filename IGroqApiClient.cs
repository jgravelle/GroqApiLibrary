
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        Task<JsonObject?> CreateChatCompletionAsync(JsonObject request);

        IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request);
    }
}