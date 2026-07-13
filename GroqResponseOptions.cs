using System;
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// <b>Beta.</b> Strongly-typed optional parameters for the Responses API
    /// (<see cref="GroqApiClient.CreateResponseAsync"/>). Only the properties you set are sent.
    /// <para>
    /// Groq does not support stateful conversations, so there is no <c>previous_response_id</c>/<c>store</c>
    /// here. Fields verified against console.groq.com/docs/responses-api (2026-07-13); this surface is beta
    /// and may change with the upstream API.
    /// </para>
    /// </summary>
    public class GroqResponseOptions
    {
        /// <summary>System-level guidance for the model (<c>instructions</c>).</summary>
        public string? Instructions { get; set; }

        /// <summary>Tool definitions (<c>tools</c>) — e.g. <c>code_interpreter</c>, <c>browser_search</c>, MCP.</summary>
        public JsonArray? Tools { get; set; }

        /// <summary>Tool selection strategy (<c>tool_choice</c>), e.g. "auto" or "required".</summary>
        public JsonNode? ToolChoice { get; set; }

        /// <summary>Reasoning effort ("low"/"medium"/"high"), sent as <c>reasoning.effort</c>.</summary>
        public string? ReasoningEffort { get; set; }

        /// <summary>Output formatting (<c>text</c>), e.g. a <c>format</c> object for JSON-schema output.</summary>
        public JsonObject? Text { get; set; }

        /// <summary>Sampling temperature.</summary>
        public double? Temperature { get; set; }

        /// <summary>Nucleus sampling probability mass (<c>top_p</c>).</summary>
        public double? TopP { get; set; }

        /// <summary>Maximum tokens to generate (<c>max_output_tokens</c>).</summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>Custom metadata (<c>metadata</c>) echoed back on the response.</summary>
        public JsonObject? Metadata { get; set; }

        /// <summary>Merges the set options into <paramref name="request"/> and returns it.</summary>
        public JsonObject ApplyTo(JsonObject request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (!string.IsNullOrEmpty(Instructions)) request["instructions"] = Instructions;
            if (Tools is not null) request["tools"] = Tools.DeepClone();
            if (ToolChoice is not null) request["tool_choice"] = ToolChoice.DeepClone();
            if (!string.IsNullOrEmpty(ReasoningEffort))
                request["reasoning"] = new JsonObject { ["effort"] = ReasoningEffort };
            if (Text is not null) request["text"] = Text.DeepClone();
            if (Temperature.HasValue) request["temperature"] = Temperature.Value;
            if (TopP.HasValue) request["top_p"] = TopP.Value;
            if (MaxOutputTokens.HasValue) request["max_output_tokens"] = MaxOutputTokens.Value;
            if (Metadata is not null) request["metadata"] = Metadata.DeepClone();

            return request;
        }
    }
}
