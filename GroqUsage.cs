using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// Token usage, prompt-cache hits and timing for a chat completion, parsed from a response's
    /// <c>usage</c> object (and top-level <c>x_groq.id</c>).
    /// Verified against console.groq.com/docs as of 2026-07-12.
    /// </summary>
    public class GroqUsage
    {
        /// <summary>Input (prompt) tokens billed.</summary>
        public int PromptTokens { get; set; }

        /// <summary>Output (completion) tokens billed.</summary>
        public int CompletionTokens { get; set; }

        /// <summary>Total tokens billed.</summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Cached prompt tokens (from usage.prompt_tokens_details.cached_tokens). Prompt caching is
        /// automatic on supported models and bills cached input at a discount; 0 when there is no hit.
        /// </summary>
        public int CachedTokens { get; set; }

        /// <summary>Seconds the request spent queued.</summary>
        public double? QueueTime { get; set; }

        /// <summary>Seconds spent processing the prompt.</summary>
        public double? PromptTime { get; set; }

        /// <summary>Seconds spent generating the completion.</summary>
        public double? CompletionTime { get; set; }

        /// <summary>Total server-side seconds.</summary>
        public double? TotalTime { get; set; }

        /// <summary>Groq request id (response x_groq.id), useful for tracing/support.</summary>
        public string? RequestId { get; set; }

        /// <summary>Fraction of prompt tokens served from cache (0-1). 0 when there is no prompt cache hit.</summary>
        public double CacheHitRatio => PromptTokens > 0 ? (double)CachedTokens / PromptTokens : 0d;

        /// <summary>
        /// Extracts usage/analytics from a chat-completion response, or returns null if the response
        /// carries no <c>usage</c> object.
        /// </summary>
        public static GroqUsage? FromResponse(JsonObject? response)
        {
            if (response is null) return null;
            if (response["usage"] is not JsonObject usage) return null;

            var result = new GroqUsage
            {
                // Chat completions use prompt_tokens/completion_tokens; the Responses API uses
                // input_tokens/output_tokens. Fall back so this parser works for both.
                PromptTokens = usage.ContainsKey("prompt_tokens") ? GetInt(usage, "prompt_tokens") : GetInt(usage, "input_tokens"),
                CompletionTokens = usage.ContainsKey("completion_tokens") ? GetInt(usage, "completion_tokens") : GetInt(usage, "output_tokens"),
                TotalTokens = GetInt(usage, "total_tokens"),
                QueueTime = GetDouble(usage, "queue_time"),
                PromptTime = GetDouble(usage, "prompt_time"),
                CompletionTime = GetDouble(usage, "completion_time"),
                TotalTime = GetDouble(usage, "total_time"),
                RequestId = (response["x_groq"] as JsonObject)?["id"]?.GetValue<string>()
            };

            if (usage["prompt_tokens_details"] is JsonObject details)
                result.CachedTokens = GetInt(details, "cached_tokens");
            else if (usage["input_tokens_details"] is JsonObject responsesDetails)
                result.CachedTokens = GetInt(responsesDetails, "cached_tokens");

            return result;
        }

        private static int GetInt(JsonObject o, string key)
            => o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

        private static double? GetDouble(JsonObject o, string key)
            => o[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    }
}
