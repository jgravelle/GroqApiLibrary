using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// Valid target endpoints for a batch (the per-line <c>url</c> and the batch <c>endpoint</c>).
    /// Note these are the OpenAI-style <c>/v1/...</c> paths, not <c>/openai/v1/...</c>.
    /// </summary>
    public static class BatchEndpoints
    {
        public const string ChatCompletions = "/v1/chat/completions";
        public const string Transcriptions = "/v1/audio/transcriptions";
        public const string Translations = "/v1/audio/translations";
    }

    /// <summary>
    /// Builds a JSONL batch-input file from individual requests, ready to hand to
    /// <see cref="GroqApiClient.UploadFileAsync"/>. Each request becomes one line
    /// <c>{ custom_id, method, url, body }</c>.
    /// </summary>
    public sealed class BatchRequestBuilder
    {
        private readonly string _url;
        private readonly List<JsonObject> _lines = new();

        /// <param name="url">Target endpoint applied to every request, e.g. <see cref="BatchEndpoints.ChatCompletions"/>.</param>
        public BatchRequestBuilder(string url = BatchEndpoints.ChatCompletions) => _url = url;

        /// <summary>Number of requests added so far.</summary>
        public int Count => _lines.Count;

        /// <summary>
        /// Adds one request. <paramref name="customId"/> must be unique within the batch and is echoed
        /// back on the corresponding output line so you can correlate results.
        /// </summary>
        public BatchRequestBuilder Add(string customId, JsonObject body, string method = "POST")
        {
            _lines.Add(new JsonObject
            {
                ["custom_id"] = customId,
                ["method"] = method,
                ["url"] = _url,
                // Clone so a body that is already parented elsewhere is safe to pass.
                ["body"] = JsonNode.Parse(body.ToJsonString())
            });
            return this;
        }

        /// <summary>Serializes the accumulated requests to JSONL (one JSON object per line).</summary>
        public string Build() => string.Join("\n", _lines.Select(l => l.ToJsonString()));

        /// <summary>Serializes to UTF-8 JSONL bytes.</summary>
        public byte[] BuildBytes() => Encoding.UTF8.GetBytes(Build());

        /// <summary>Serializes to a UTF-8 JSONL stream, suitable for <see cref="GroqApiClient.UploadFileAsync"/>.</summary>
        public Stream BuildStream() => new MemoryStream(BuildBytes());
    }

    /// <summary>One parsed line of a batch output file.</summary>
    public sealed class BatchOutputLine
    {
        /// <summary>Provider-assigned id for this result line.</summary>
        public string? Id { get; set; }

        /// <summary>The <c>custom_id</c> supplied in the input, for correlation.</summary>
        public string? CustomId { get; set; }

        /// <summary>HTTP status code of the individual request.</summary>
        public int? StatusCode { get; set; }

        /// <summary>Groq request id for the individual request.</summary>
        public string? RequestId { get; set; }

        /// <summary>The response body (e.g. a chat-completion object). Null on error lines.</summary>
        public JsonObject? Body { get; set; }

        /// <summary>Error detail when the request failed; null on success.</summary>
        public JsonNode? Error { get; set; }

        /// <summary>True when there was no error and the status code is 2xx.</summary>
        public bool IsSuccess => Error is null && StatusCode is >= 200 and < 300;
    }

    /// <summary>Helpers for reading batch output JSONL (from the output/error file content).</summary>
    public static class BatchJsonl
    {
        /// <summary>Parses batch output JSONL into typed lines. Blank lines are skipped.</summary>
        public static List<BatchOutputLine> ParseOutput(string jsonl)
        {
            var result = new List<BatchOutputLine>();
            if (string.IsNullOrEmpty(jsonl)) return result;

            foreach (var raw in jsonl.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (JsonNode.Parse(line) is not JsonObject o) continue;

                var resp = o["response"] as JsonObject;
                result.Add(new BatchOutputLine
                {
                    Id = o["id"]?.GetValue<string>(),
                    CustomId = o["custom_id"]?.GetValue<string>(),
                    StatusCode = resp?["status_code"] is JsonValue sc && sc.TryGetValue<int>(out var i) ? i : null,
                    RequestId = resp?["request_id"]?.GetValue<string>(),
                    Body = resp?["body"] as JsonObject,
                    Error = o["error"]
                });
            }
            return result;
        }

        /// <summary>Parses batch output JSONL from raw UTF-8 bytes (e.g. from <see cref="GroqApiClient.GetFileContentAsync"/>).</summary>
        public static List<BatchOutputLine> ParseOutput(byte[] bytes) => ParseOutput(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Strongly-typed view of a batch object, parsed from the create/retrieve response.
    /// Verified against console.groq.com/docs/batch as of 2026-07-12.
    /// </summary>
    public sealed class GroqBatch
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? Endpoint { get; set; }
        public string? CompletionWindow { get; set; }
        public string? InputFileId { get; set; }
        public string? OutputFileId { get; set; }
        public string? ErrorFileId { get; set; }

        public int RequestsTotal { get; set; }
        public int RequestsCompleted { get; set; }
        public int RequestsFailed { get; set; }

        public long? CreatedAt { get; set; }
        public long? ExpiresAt { get; set; }
        public long? CompletedAt { get; set; }
        public long? FailedAt { get; set; }
        public long? CancelledAt { get; set; }
        public long? ExpiredAt { get; set; }

        /// <summary>Raw errors object from the batch, if any.</summary>
        public JsonNode? Errors { get; set; }

        /// <summary>True once the batch has reached a terminal state and will not change further.</summary>
        public bool IsTerminal => Status is "completed" or "failed" or "expired" or "cancelled";

        /// <summary>True when the batch finished successfully.</summary>
        public bool IsCompleted => Status == "completed";

        public static GroqBatch? FromResponse(JsonObject? o)
        {
            if (o is null) return null;
            var counts = o["request_counts"] as JsonObject;
            return new GroqBatch
            {
                Id = o["id"]?.GetValue<string>(),
                Status = o["status"]?.GetValue<string>(),
                Endpoint = o["endpoint"]?.GetValue<string>(),
                CompletionWindow = o["completion_window"]?.GetValue<string>(),
                InputFileId = o["input_file_id"]?.GetValue<string>(),
                OutputFileId = o["output_file_id"]?.GetValue<string>(),
                ErrorFileId = o["error_file_id"]?.GetValue<string>(),
                RequestsTotal = GetInt(counts, "total"),
                RequestsCompleted = GetInt(counts, "completed"),
                RequestsFailed = GetInt(counts, "failed"),
                CreatedAt = GetLong(o, "created_at"),
                ExpiresAt = GetLong(o, "expires_at"),
                CompletedAt = GetLong(o, "completed_at"),
                FailedAt = GetLong(o, "failed_at"),
                CancelledAt = GetLong(o, "cancelled_at"),
                ExpiredAt = GetLong(o, "expired_at"),
                Errors = o["errors"]
            };
        }

        private static int GetInt(JsonObject? o, string key)
            => o?[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

        private static long? GetLong(JsonObject o, string key)
            => o[key] is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;
    }
}
