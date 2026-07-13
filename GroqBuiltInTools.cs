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

        /// <summary>
        /// <b>Beta.</b> A remote MCP server tool entry (<c>{ "type": "mcp", ... }</c>) for the <c>tools</c>
        /// array. Groq's backend connects to <paramref name="serverUrl"/> and invokes its tools
        /// server-side, so the URL must be reachable from the public internet over HTTPS (a localhost or
        /// LAN server will not work). Works on both Chat Completions and the Responses API.
        /// <para>
        /// Pass credentials the target server needs via <paramref name="headers"/> (e.g.
        /// <c>{ ["Authorization"] = "Bearer &lt;token&gt;" }</c>) — note these are forwarded to Groq, which
        /// relays them to the server. Use <paramref name="allowedTools"/> to restrict which of the server's
        /// tools the model may call, and <paramref name="requireApproval"/> (<see cref="Approval"/>) to gate
        /// execution.
        /// </para>
        /// Remote MCP is beta on Groq; this surface may change. Verified against console.groq.com/docs
        /// (MCP tool) on 2026-07-13.
        /// </summary>
        public static JsonObject Mcp(
            string serverLabel,
            string serverUrl,
            IDictionary<string, string>? headers = null,
            string? serverDescription = null,
            string? requireApproval = null,
            IEnumerable<string>? allowedTools = null)
        {
            if (string.IsNullOrEmpty(serverLabel)) throw new ArgumentException("A server label is required.", nameof(serverLabel));
            if (string.IsNullOrEmpty(serverUrl)) throw new ArgumentException("A server URL is required.", nameof(serverUrl));

            var tool = new JsonObject
            {
                ["type"] = Types.Mcp,
                ["server_label"] = serverLabel,
                ["server_url"] = serverUrl,
            };

            if (headers is { Count: > 0 })
            {
                var headerObject = new JsonObject();
                foreach (var header in headers)
                    headerObject[header.Key] = header.Value;
                tool["headers"] = headerObject;
            }

            if (!string.IsNullOrEmpty(serverDescription)) tool["server_description"] = serverDescription;
            if (!string.IsNullOrEmpty(requireApproval)) tool["require_approval"] = requireApproval;

            if (allowedTools is not null)
            {
                var allowed = new JsonArray();
                foreach (var name in allowedTools) allowed.Add(name);
                tool["allowed_tools"] = allowed;
            }

            return tool;
        }

        /// <summary>The <c>type</c> values for built-in tool entries.</summary>
        public static class Types
        {
            public const string BrowserSearch = "browser_search";
            public const string CodeInterpreter = "code_interpreter";
            public const string Mcp = "mcp";
        }

        /// <summary><c>require_approval</c> values for a remote MCP tool.</summary>
        public static class Approval
        {
            public const string Never = "never";
            public const string Always = "always";
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
