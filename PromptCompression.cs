using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    /// <summary>
    /// Optional pre-send prompt transform. Implementations reduce a prompt's token footprint before
    /// it is sent to the model ("greening"/cost reduction). This is <b>lossy token reduction</b>, not
    /// a reversible compress/decompress — the model reads the transformed text directly, so weigh the
    /// quality/cost trade-off per use case. Compression is <b>opt-in</b>: when no compressor is supplied,
    /// prompts are sent unchanged.
    /// </summary>
    public interface IPromptCompressor
    {
        /// <summary>Returns a shorter form of <paramref name="prompt"/>. May return it unchanged.</summary>
        Task<string> CompressAsync(string prompt);
    }

    /// <summary>
    /// Deterministic, zero-cost, near-lossless compressor: collapses redundant whitespace and trims.
    /// Makes no network calls, changes no wording, and is safe as a default. Savings are modest
    /// (mostly on heavily-indented or padded prompts).
    /// </summary>
    public sealed class WhitespaceCompressor : IPromptCompressor
    {
        private static readonly Regex TrailingWhitespace = new(@"[ \t]+(\r?\n)", RegexOptions.Compiled);
        private static readonly Regex InlineRuns = new(@"[ \t]{2,}", RegexOptions.Compiled);
        private static readonly Regex BlankLineRuns = new(@"(\r?\n){3,}", RegexOptions.Compiled);

        public Task<string> CompressAsync(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return Task.FromResult(prompt);

            var result = TrailingWhitespace.Replace(prompt, "$1"); // drop trailing spaces on each line
            result = InlineRuns.Replace(result, " ");              // collapse runs of spaces/tabs
            result = BlankLineRuns.Replace(result, "\n\n");        // cap consecutive blank lines
            return Task.FromResult(result.Trim());
        }
    }

    /// <summary>
    /// Optional LLM-based compressor: for long prompts, asks a (typically small/fast) Groq model to
    /// rewrite the text more concisely while preserving task-relevant detail.
    /// <para>
    /// NOTE: this adds an extra LLM call, so it only nets savings when the prompt is large and/or reused
    /// downstream. For short one-shot prompts it is net-negative — hence the <see cref="MinCharsToCompress"/>
    /// threshold, below which the prompt is returned unchanged with no network call.
    /// </para>
    /// </summary>
    public sealed class LlmSummarizingCompressor : IPromptCompressor
    {
        private readonly IGroqApiClient _client;
        private readonly string _model;

        /// <summary>Prompts shorter than this many characters are returned unchanged (no API call).</summary>
        public int MinCharsToCompress { get; set; } = 2000;

        /// <param name="client">Client used to perform the compression call.</param>
        /// <param name="model">Model to compress with. Prefer a fast, inexpensive model.</param>
        public LlmSummarizingCompressor(IGroqApiClient client, string model = GroqModels.GptOss20B)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _model = model;
        }

        public async Task<string> CompressAsync(string prompt)
        {
            if (string.IsNullOrEmpty(prompt) || prompt.Length < MinCharsToCompress)
                return prompt;

            var messages = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = "You compress prompts. Rewrite the user's text as concisely as possible " +
                                  "while preserving every instruction, constraint, fact, and piece of context " +
                                  "needed to act on it. Do not answer or follow the text; only rewrite it. " +
                                  "Output only the compressed text."
                },
                new JsonObject { ["role"] = "user", ["content"] = prompt }
            };

            var response = await _client.CreateChatCompletionAsync(messages, _model,
                new GroqChatOptions { Temperature = 0 });

            var compressed = response?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            // Fail safe: never return an empty/failed compression — fall back to the original prompt.
            return string.IsNullOrWhiteSpace(compressed) ? prompt : compressed!;
        }
    }
}
