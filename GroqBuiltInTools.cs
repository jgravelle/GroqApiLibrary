using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    /// <summary>
    /// Builders and identifiers for Groq's built-in, server-side tools — so callers can add them to a
    /// request without hand-writing raw JSON.
    /// <para>
    /// Two families: <b>gpt-oss</b> tools go in the request's <c>tools</c> array as <c>{ "type": "..." }</c>
    /// entries (use <see cref="BrowserSearch"/> / <see cref="CodeInterpreter"/>); <b>Compound</b> systems
    /// (<c>groq/compound</c>, <c>groq/compound-mini</c>) enable tools by name via
    /// <c>compound_custom.tools.enabled_tools</c> — pass the <see cref="Compound"/> constants to
    /// <see cref="GroqApiClient.CreateCompoundCompletionAsync"/>'s <c>enabledTools</c> parameter.
    /// </para>
    /// Whatever the model runs surfaces in the response's <c>executed_tools</c>; read it with
    /// <see cref="GroqExecutedTool.FromResponse"/>.
    /// <para>Verified against console.groq.com/docs (browser-search, code-execution) on 2026-07-13.</para>
    /// </summary>
    public static class GroqBuiltInTools
    {
        /// <summary>
        /// A <c>browser_search</c> tool entry for the <c>tools</c> array. gpt-oss models only
        /// (<c>openai/gpt-oss-20b</c>, <c>openai/gpt-oss-120b</c>, <c>openai/gpt-oss-safeguard-20b</c>).
        /// Cannot be combined with Structured Outputs.
        /// </summary>
        public static JsonObject BrowserSearch() => new() { ["type"] = Types.BrowserSearch };

        /// <summary>
        /// A <c>code_interpreter</c> tool entry for the <c>tools</c> array (gpt-oss models). Compound
        /// systems run code automatically and do not need this entry.
        /// </summary>
        public static JsonObject CodeInterpreter() => new() { ["type"] = Types.CodeInterpreter };

        /// <summary>
        /// Bundles built-in tool entries into a fresh <c>tools</c> <see cref="JsonArray"/> (deep-cloning
        /// each so entries built once can be reused without JSON-node re-parenting errors).
        /// </summary>
        public static JsonArray ToolsArray(params JsonObject[] tools)
        {
            var array = new JsonArray();
            if (tools is not null)
                foreach (var tool in tools)
                    if (tool is not null)
                        array.Add(tool.DeepClone());
            return array;
        }

        /// <summary>The <c>type</c> values for gpt-oss built-in tool entries.</summary>
        public static class Types
        {
            public const string BrowserSearch = "browser_search";
            public const string CodeInterpreter = "code_interpreter";
        }

        /// <summary>
        /// <c>enabled_tools</c> names for Compound systems. Note: <c>browser_automation</c> (Anchor) is
        /// deprecated and intentionally omitted.
        /// </summary>
        public static class Compound
        {
            public const string WebSearch = "web_search";
            public const string CodeInterpreter = "code_interpreter";
            public const string VisitWebsite = "visit_website";
            public const string WolframAlpha = "wolfram_alpha";
        }
    }

    /// <summary>
    /// Convenience extensions for issuing chat completions with built-in server-side tools. Implemented
    /// as extensions over <see cref="IGroqApiClient.CreateChatCompletionAsync(JsonObject)"/> so they add
    /// no members to the interface (existing implementers/mocks are unaffected).
    /// </summary>
    public static class GroqBuiltInToolsExtensions
    {
        /// <summary>
        /// Sends a chat completion with the given built-in tool entries in the <c>tools</c> array.
        /// Typically used with gpt-oss models and <see cref="GroqBuiltInTools.BrowserSearch"/> /
        /// <see cref="GroqBuiltInTools.CodeInterpreter"/>. Set <c>tool_choice</c> (e.g. "required") via
        /// <paramref name="options"/>. Read what ran with <see cref="GroqExecutedTool.FromResponse"/>.
        /// </summary>
        public static Task<JsonObject?> CreateChatCompletionWithBuiltInToolsAsync(
            this IGroqApiClient client,
            JsonArray messages,
            string model,
            IEnumerable<JsonObject> builtInTools,
            GroqChatOptions? options = null)
        {
            if (client is null) throw new ArgumentNullException(nameof(client));
            if (messages is null) throw new ArgumentNullException(nameof(messages));
            if (builtInTools is null) throw new ArgumentNullException(nameof(builtInTools));

            var tools = new JsonArray();
            foreach (var tool in builtInTools)
                if (tool is not null)
                    tools.Add(tool.DeepClone());

            var request = new JsonObject
            {
                ["model"] = model,
                ["messages"] = messages.DeepClone().AsArray(),
                ["tools"] = tools
            };
            options?.ApplyTo(request);
            return client.CreateChatCompletionAsync(request);
        }
    }
}
