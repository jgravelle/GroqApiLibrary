using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    /// <summary>
    /// Base type for errors returned by the Groq API (non-2xx responses).
    /// <para>
    /// Derives from <see cref="HttpRequestException"/> so existing code that catches
    /// <c>HttpRequestException</c> continues to work unchanged; new code can catch the more specific
    /// subtypes (<see cref="GroqRateLimitException"/>, <see cref="GroqAuthenticationException"/>, etc.)
    /// and read the parsed <see cref="ErrorCode"/>/<see cref="ErrorType"/>/<see cref="ResponseBody"/>.
    /// </para>
    /// </summary>
    public class GroqApiException : HttpRequestException
    {
        /// <summary>The <c>error.code</c> from the Groq error envelope, when present.</summary>
        public string? ErrorCode { get; }

        /// <summary>The <c>error.type</c> from the Groq error envelope, when present.</summary>
        public string? ErrorType { get; }

        /// <summary>The raw response body, for diagnostics or fields not surfaced as properties.</summary>
        public string? ResponseBody { get; }

        public GroqApiException(
            string message,
            HttpStatusCode? statusCode = null,
            string? errorCode = null,
            string? errorType = null,
            string? responseBody = null)
            : base(message, null, statusCode)
        {
            ErrorCode = errorCode;
            ErrorType = errorType;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// Maps an HTTP status code + response body to the most specific exception type, parsing Groq's
        /// <c>{ "error": { "message", "type", "code" } }</c> envelope when present. The message preserves
        /// the legacy shape (<c>"... failed with status code {N}. ... Response content: {body}"</c>) so
        /// any code that matched on <see cref="Exception.Message"/> keeps working.
        /// </summary>
        internal static GroqApiException Create(
            HttpStatusCode statusCode,
            string? responseBody,
            string operation,
            TimeSpan? retryAfter = null)
        {
            var (code, type, apiMessage) = ParseError(responseBody);

            var detail = string.IsNullOrEmpty(apiMessage) ? string.Empty : $" {apiMessage}";
            var message = $"{operation} failed with status code {statusCode}.{detail} Response content: {responseBody}";

            return (int)statusCode switch
            {
                400 => new GroqBadRequestException(message, statusCode, code, type, responseBody),
                401 => new GroqAuthenticationException(message, statusCode, code, type, responseBody),
                403 => new GroqPermissionException(message, statusCode, code, type, responseBody),
                404 => new GroqNotFoundException(message, statusCode, code, type, responseBody),
                429 => new GroqRateLimitException(message, statusCode, code, type, responseBody, retryAfter),
                >= 500 => new GroqServerException(message, statusCode, code, type, responseBody),
                _ => new GroqApiException(message, statusCode, code, type, responseBody),
            };
        }

        private static (string? code, string? type, string? message) ParseError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return (null, null, null);

            try
            {
                if (JsonNode.Parse(body) is JsonObject root && root["error"] is JsonObject error)
                    return (AsString(error["code"]), AsString(error["type"]), AsString(error["message"]));
            }
            catch (JsonException)
            {
                // Non-JSON error body (e.g. an HTML gateway page) — fall through to null fields.
            }

            return (null, null, null);
        }

        // GetValue<string>() throws if the node is a non-string JSON value; be tolerant of numeric codes.
        private static string? AsString(JsonNode? node)
        {
            if (node is not JsonValue value)
                return null;
            return value.TryGetValue<string>(out var s) ? s : value.ToString();
        }
    }

    /// <summary>HTTP 400 — malformed request (bad parameters, invalid schema, etc.).</summary>
    public sealed class GroqBadRequestException : GroqApiException
    {
        public GroqBadRequestException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null)
            : base(message, statusCode, errorCode, errorType, responseBody) { }
    }

    /// <summary>HTTP 401 — missing or invalid API key.</summary>
    public sealed class GroqAuthenticationException : GroqApiException
    {
        public GroqAuthenticationException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null)
            : base(message, statusCode, errorCode, errorType, responseBody) { }
    }

    /// <summary>HTTP 403 — authenticated but not permitted (e.g. an endpoint not available for the plan).</summary>
    public sealed class GroqPermissionException : GroqApiException
    {
        public GroqPermissionException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null)
            : base(message, statusCode, errorCode, errorType, responseBody) { }
    }

    /// <summary>HTTP 404 — the requested resource (model, file, batch, …) was not found.</summary>
    public sealed class GroqNotFoundException : GroqApiException
    {
        public GroqNotFoundException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null)
            : base(message, statusCode, errorCode, errorType, responseBody) { }
    }

    /// <summary>HTTP 429 — rate limited. Inspect <see cref="RetryAfter"/> to back off.</summary>
    public sealed class GroqRateLimitException : GroqApiException
    {
        /// <summary>The <c>Retry-After</c> delay from the response headers, when the server supplied one.</summary>
        public TimeSpan? RetryAfter { get; }

        public GroqRateLimitException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null,
            TimeSpan? retryAfter = null)
            : base(message, statusCode, errorCode, errorType, responseBody)
            => RetryAfter = retryAfter;
    }

    /// <summary>HTTP 5xx — a server-side error; typically transient and safe to retry.</summary>
    public sealed class GroqServerException : GroqApiException
    {
        public GroqServerException(string message, HttpStatusCode? statusCode = null,
            string? errorCode = null, string? errorType = null, string? responseBody = null)
            : base(message, statusCode, errorCode, errorType, responseBody) { }
    }
}
