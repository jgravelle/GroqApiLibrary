using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// A single entry from a response's <c>executed_tools</c> array — one server-side tool the model ran
    /// (web search, code interpreter, etc.). Works for both Compound systems and gpt-oss built-in tools,
    /// which share the same <c>choices[0].message.executed_tools</c> location.
    /// <para>
    /// Field names vary slightly by tool/model (e.g. code execution reports <c>type: "python"</c> with
    /// <c>output</c>, others use <c>name</c>/<c>code_results</c>), so accessors are best-effort and
    /// <see cref="Raw"/> exposes the original JSON for anything not surfaced here.
    /// </para>
    /// </summary>
    public sealed record GroqExecutedTool
    {
        /// <summary>Position of this tool in the executed_tools array, when present.</summary>
        public int? Index { get; init; }

        /// <summary>The tool <c>type</c> (e.g. "python", "browser_search"), when present.</summary>
        public string? Type { get; init; }

        /// <summary>The tool <c>name</c>, when present.</summary>
        public string? Name { get; init; }

        /// <summary>The tool <c>arguments</c> (often a JSON string, e.g. the generated code), when present.</summary>
        public string? Arguments { get; init; }

        /// <summary>The tool <c>output</c> / <c>code_results</c>, when present.</summary>
        public string? Output { get; init; }

        /// <summary>The original JSON object for this entry — escape hatch for tool-specific fields.</summary>
        public JsonObject Raw { get; init; } = new();

        /// <summary>Projects a single <c>executed_tools</c> element into a <see cref="GroqExecutedTool"/>.</summary>
        public static GroqExecutedTool From(JsonObject entry) => new()
        {
            Index = AsInt(entry["index"]),
            Type = AsString(entry["type"]),
            Name = AsString(entry["name"]),
            Arguments = AsString(entry["arguments"]),
            Output = AsString(entry["output"]) ?? AsString(entry["code_results"]),
            Raw = entry,
        };

        /// <summary>
        /// Reads and projects the <c>executed_tools</c> array from a chat/compound response. Returns an
        /// empty list when none is present.
        /// </summary>
        public static IReadOnlyList<GroqExecutedTool> FromResponse(JsonObject? response)
        {
            if (response?["choices"]?[0]?["message"]?["executed_tools"] is not JsonArray array)
                return Array.Empty<GroqExecutedTool>();

            var list = new List<GroqExecutedTool>(array.Count);
            foreach (var node in array)
                if (node is JsonObject entry)
                    list.Add(From(entry));
            return list;
        }

        // Tolerant readers: GetValue<T>() throws on a type mismatch, so fall back to a JSON rendering.
        private static string? AsString(JsonNode? node) => node switch
        {
            null => null,
            JsonValue value => value.TryGetValue<string>(out var s) ? s : value.ToString(),
            _ => node.ToJsonString(),
        };

        private static int? AsInt(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<int>(out var i) ? i : null;
    }
}
