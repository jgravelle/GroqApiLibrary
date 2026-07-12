using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        #region Chat Completions

        /// <summary>
        /// Creates a chat completion with the given request.
        /// </summary>
        Task<JsonObject?> CreateChatCompletionAsync(JsonObject request);

        /// <summary>
        /// Creates a chat completion from messages plus optional strongly-typed request parameters
        /// (see <see cref="GroqChatOptions"/>). Read usage/analytics with <see cref="GroqUsage.FromResponse"/>.
        /// </summary>
        Task<JsonObject?> CreateChatCompletionAsync(JsonArray messages, string model, GroqChatOptions? options = null);

        /// <summary>
        /// Creates a streaming chat completion.
        /// </summary>
        IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request);

        /// <summary>
        /// Streams a chat completion from messages plus optional parameters (see <see cref="GroqChatOptions"/>).
        /// Set <see cref="GroqChatOptions.StreamIncludeUsage"/> to receive a final chunk carrying usage.
        /// </summary>
        IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonArray messages, string model, GroqChatOptions? options = null);

        /// <summary>
        /// Creates a chat completion with structured JSON output using JSON Schema.
        /// </summary>
        Task<JsonObject?> CreateChatCompletionWithStructuredOutputAsync(
            JsonArray messages,
            string model,
            JsonObject jsonSchema,
            string schemaName = "response",
            bool strict = false,
            float? temperature = null,
            int? maxCompletionTokens = null);

        /// <summary>
        /// Creates a chat completion with reasoning/thinking support.
        /// </summary>
        Task<JsonObject?> CreateChatCompletionWithReasoningAsync(
            JsonArray messages,
            string model,
            string? reasoningEffort = null,
            string? reasoningFormat = null,
            bool? includeReasoning = null,
            float? temperature = null,
            int? maxCompletionTokens = null);

        /// <summary>
        /// Creates a chat completion with document context for RAG-style applications.
        /// </summary>
        Task<JsonObject?> CreateChatCompletionWithDocumentsAsync(
            JsonArray messages,
            string model,
            JsonArray documents,
            bool enableCitations = true,
            float? temperature = null,
            int? maxCompletionTokens = null);

        /// <summary>
        /// Creates a completion using Compound systems with built-in web search and code execution.
        /// </summary>
        Task<JsonObject?> CreateCompoundCompletionAsync(
            JsonArray messages,
            bool useMini = false,
            SearchSettings? searchSettings = null,
            float? temperature = null,
            int? maxCompletionTokens = null,
            string[]? enabledTools = null);

        #endregion

        #region Audio

        /// <summary>
        /// Transcribes audio to text using Whisper models.
        /// </summary>
        Task<JsonObject?> CreateTranscriptionAsync(
            Stream audioFile, 
            string fileName, 
            string model,
            string? prompt = null, 
            string responseFormat = "json", 
            string? language = null, 
            float? temperature = null);

        /// <summary>
        /// Translates audio to English text using Whisper models.
        /// </summary>
        Task<JsonObject?> CreateTranslationAsync(
            Stream audioFile,
            string fileName,
            string model,
            string? prompt = null,
            string responseFormat = "json",
            float? temperature = null);

        /// <summary>
        /// Transcribes audio from a publicly accessible URL (no upload) using Whisper models.
        /// </summary>
        Task<JsonObject?> CreateTranscriptionFromUrlAsync(
            string url,
            string model,
            string? prompt = null,
            string responseFormat = "json",
            string? language = null,
            float? temperature = null);

        /// <summary>
        /// Translates audio from a publicly accessible URL (no upload) to English using Whisper models.
        /// </summary>
        Task<JsonObject?> CreateTranslationFromUrlAsync(
            string url,
            string model,
            string? prompt = null,
            string responseFormat = "json",
            float? temperature = null);

        /// <summary>
        /// Converts text to speech audio using Orpheus TTS models.
        /// </summary>
        Task<byte[]> CreateSpeechAsync(
            string text,
            string voice,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav");

        /// <summary>
        /// Converts text to speech and saves to a file.
        /// </summary>
        Task CreateSpeechToFileAsync(
            string text,
            string voice,
            string outputPath,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav");

        /// <summary>
        /// Converts text to speech and returns as a stream.
        /// </summary>
        Task<Stream> CreateSpeechStreamAsync(
            string text,
            string voice,
            string model = "canopylabs/orpheus-v1-english",
            string responseFormat = "wav");

        #endregion

        #region Vision

        /// <summary>
        /// Creates a vision completion with the given request.
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionAsync(JsonObject request);

        /// <summary>
        /// Creates a vision completion with an image URL.
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionWithImageUrlAsync(
            string imageUrl,
            string prompt,
            string model = "qwen/qwen3.6-27b",
            float? temperature = null);

        /// <summary>
        /// Creates a vision completion with a local image file (base64 encoded).
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionWithBase64ImageAsync(
            string imagePath,
            string prompt,
            string model = "qwen/qwen3.6-27b",
            float? temperature = null);

        /// <summary>
        /// Creates a vision completion with tool calling support.
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionWithToolsAsync(
            string imageUrl,
            string prompt,
            List<Tool> tools,
            string model = "qwen/qwen3.6-27b",
            bool parallelToolCalls = true);

        /// <summary>
        /// Creates a vision completion with JSON mode response.
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionWithJsonModeAsync(
            string imageUrl,
            string prompt,
            string model = "qwen/qwen3.6-27b");

        /// <summary>
        /// Creates a vision completion with structured JSON output.
        /// </summary>
        Task<JsonObject?> CreateVisionCompletionWithStructuredOutputAsync(
            string imageUrl,
            string prompt,
            JsonObject jsonSchema,
            string schemaName = "response",
            bool strict = false,
            string model = "qwen/qwen3.6-27b");

        #endregion

        #region Models

        /// <summary>
        /// Lists all available models.
        /// </summary>
        Task<JsonObject?> ListModelsAsync();

        #endregion

        #region Files

        /// <summary>
        /// Uploads a file (JSONL, up to 100 MB) for use with the Batch API.
        /// </summary>
        Task<JsonObject?> UploadFileAsync(Stream file, string fileName, string purpose = "batch");

        /// <summary>
        /// Lists the files owned by the account.
        /// </summary>
        Task<JsonObject?> ListFilesAsync();

        /// <summary>
        /// Retrieves metadata for a single file.
        /// </summary>
        Task<JsonObject?> GetFileAsync(string fileId);

        /// <summary>
        /// Downloads the raw content of a file as bytes.
        /// </summary>
        Task<byte[]> GetFileContentAsync(string fileId);

        /// <summary>
        /// Downloads the raw content of a file as a stream.
        /// </summary>
        Task<Stream> GetFileContentStreamAsync(string fileId);

        /// <summary>
        /// Deletes a file.
        /// </summary>
        Task<JsonObject?> DeleteFileAsync(string fileId);

        #endregion

        #region Batch

        /// <summary>
        /// Creates a batch job over an uploaded JSONL input file. Endpoint values are in <see cref="BatchEndpoints"/>.
        /// </summary>
        Task<JsonObject?> CreateBatchAsync(string inputFileId, string endpoint,
            string completionWindow = "24h", JsonObject? metadata = null);

        /// <summary>
        /// Retrieves a batch by id (parse with <see cref="GroqBatch.FromResponse"/>).
        /// </summary>
        Task<JsonObject?> GetBatchAsync(string batchId);

        /// <summary>
        /// Lists batch jobs.
        /// </summary>
        Task<JsonObject?> ListBatchesAsync();

        /// <summary>
        /// Requests cancellation of an in-progress batch.
        /// </summary>
        Task<JsonObject?> CancelBatchAsync(string batchId);

        #endregion

        #region Tool Use

        /// <summary>
        /// Runs a conversation with tool calling, executing tools and returning the final response.
        /// </summary>
        Task<string> RunConversationWithToolsAsync(
            string userPrompt, 
            List<Tool> tools, 
            string model, 
            string systemMessage,
            bool parallelToolCalls = true,
            string? serviceTier = null);

        #endregion
    }
}
