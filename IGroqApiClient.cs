
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        Task<JsonObject?> CreateChatCompletionAsync(JsonObject request);
        IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request);
        Task<JsonObject?> CreateTranscriptionAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", string? language = null, float? temperature = null);
        Task<JsonObject?> CreateTranslationAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", float? temperature = null);
        Task<JsonObject?> ListModelsAsync();
        Task<string> RunConversationWithToolsAsync(string userPrompt, List<Tool> tools, string model, string systemMessage);
    }
}