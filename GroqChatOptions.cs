using System;
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// Optional chat-completion request parameters. Set only what you need; null properties are omitted
    /// from the request. Apply to an existing request with <see cref="ApplyTo"/>, or build a fresh
    /// request with <see cref="ToRequest"/>.
    /// Verified against console.groq.com/docs/api-reference as of 2026-07-12.
    /// </summary>
    /// <remarks>
    /// frequency_penalty, presence_penalty, logprobs and n are intentionally omitted: Groq currently
    /// accepts but ignores them (n only supports 1).
    /// </remarks>
    public class GroqChatOptions
    {
        /// <summary>Sampling temperature (0-2).</summary>
        public double? Temperature { get; set; }

        /// <summary>Nucleus sampling probability mass (0-1).</summary>
        public double? TopP { get; set; }

        /// <summary>Maximum tokens to generate. Preferred over the deprecated max_tokens.</summary>
        public int? MaxCompletionTokens { get; set; }

        /// <summary>Seed for best-effort deterministic sampling.</summary>
        public int? Seed { get; set; }

        /// <summary>Up to 4 stop sequences.</summary>
        public string[]? Stop { get; set; }

        /// <summary>Allow the model to request multiple tool calls in parallel (default true on Groq).</summary>
        public bool? ParallelToolCalls { get; set; }

        /// <summary>
        /// Tool selection: a string ("none" | "auto" | "required") or a specific tool object.
        /// The value is deep-copied when applied, so a node with an existing parent is safe to pass.
        /// </summary>
        public JsonNode? ToolChoice { get; set; }

        /// <summary>Service tier: see <see cref="ServiceTiers"/> (auto/on_demand/flex/performance).</summary>
        public string? ServiceTier { get; set; }

        /// <summary>Reasoning effort: see <see cref="GroqApiLibrary.ReasoningEffort"/>. Model-specific valid values.</summary>
        public string? ReasoningEffort { get; set; }

        /// <summary>Reasoning format: see <see cref="GroqApiLibrary.ReasoningFormat"/>. Mutually exclusive with <see cref="IncludeReasoning"/>.</summary>
        public string? ReasoningFormat { get; set; }

        /// <summary>gpt-oss only: emit reasoning as a separate field. Mutually exclusive with <see cref="ReasoningFormat"/>.</summary>
        public bool? IncludeReasoning { get; set; }

        /// <summary>Opaque end-user identifier.</summary>
        public string? User { get; set; }

        /// <summary>When streaming, request usage stats in the final chunk (stream_options.include_usage).</summary>
        public bool? StreamIncludeUsage { get; set; }

        /// <summary>Merges the set options into <paramref name="request"/> and returns it.</summary>
        public JsonObject ApplyTo(JsonObject request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (Temperature.HasValue) request["temperature"] = Temperature.Value;
            if (TopP.HasValue) request["top_p"] = TopP.Value;
            if (MaxCompletionTokens.HasValue) request["max_completion_tokens"] = MaxCompletionTokens.Value;
            if (Seed.HasValue) request["seed"] = Seed.Value;

            if (Stop is { Length: > 0 })
            {
                var arr = new JsonArray();
                foreach (var s in Stop) arr.Add(s);
                request["stop"] = arr;
            }

            if (ParallelToolCalls.HasValue) request["parallel_tool_calls"] = ParallelToolCalls.Value;
            if (ToolChoice is not null) request["tool_choice"] = Clone(ToolChoice);
            if (!string.IsNullOrEmpty(ServiceTier)) request["service_tier"] = ServiceTier;
            if (!string.IsNullOrEmpty(ReasoningEffort)) request["reasoning_effort"] = ReasoningEffort;
            if (!string.IsNullOrEmpty(ReasoningFormat)) request["reasoning_format"] = ReasoningFormat;
            if (IncludeReasoning.HasValue) request["include_reasoning"] = IncludeReasoning.Value;
            if (!string.IsNullOrEmpty(User)) request["user"] = User;

            if (StreamIncludeUsage.HasValue)
                request["stream_options"] = new JsonObject { ["include_usage"] = StreamIncludeUsage.Value };

            return request;
        }

        /// <summary>Builds a new request object from <paramref name="model"/>, <paramref name="messages"/> and these options.</summary>
        public JsonObject ToRequest(string model, JsonArray messages)
        {
            var request = new JsonObject { ["model"] = model, ["messages"] = messages };
            return ApplyTo(request);
        }

        // JsonNode.DeepClone() is only available on net9+; this library targets net8.0, so clone via round-trip.
        private static JsonNode Clone(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
    }
}
