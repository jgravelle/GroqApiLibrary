using System;
using System.Net.Http;
using GroqApiLibrary;

// Placed in the conventional DI namespace so a single `using Microsoft.Extensions.DependencyInjection;`
// (already present in typical ASP.NET Core / Generic Host projects) surfaces these extensions.
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions for registering <see cref="IGroqApiClient"/>.
    /// </summary>
    public static class GroqServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="IGroqApiClient"/> (implemented by <see cref="GroqApiClient"/>) with a
        /// pooled <see cref="HttpClient"/> managed by <see cref="IHttpClientFactory"/>, which avoids the
        /// socket-exhaustion problems of manually newing up <see cref="HttpClient"/> per request.
        /// The Groq bearer token is applied from <paramref name="apiKey"/>.
        /// </summary>
        /// <param name="services">The service collection to add the client to.</param>
        /// <param name="apiKey">The Groq API key used to authenticate requests.</param>
        /// <returns>
        /// An <see cref="IHttpClientBuilder"/> so callers can further configure the underlying client
        /// (e.g. <c>.SetHandlerLifetime(...)</c>, resilience/retry handlers).
        /// </returns>
        public static IHttpClientBuilder AddGroqApiClient(this IServiceCollection services, string apiKey)
            => services.AddGroqApiClient(apiKey, configureClient: null);

        /// <summary>
        /// Registers <see cref="IGroqApiClient"/> as above, with a hook to customize the managed
        /// <see cref="HttpClient"/> (for example to set <see cref="HttpClient.Timeout"/> or a proxy
        /// <see cref="HttpClient.BaseAddress"/>). The configuration runs before the client's auth header
        /// is applied, so callers should not overwrite the <c>Authorization</c> header here.
        /// </summary>
        /// <param name="services">The service collection to add the client to.</param>
        /// <param name="apiKey">The Groq API key used to authenticate requests.</param>
        /// <param name="configureClient">Optional callback to configure the managed <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for further configuration.</returns>
        public static IHttpClientBuilder AddGroqApiClient(
            this IServiceCollection services,
            string apiKey,
            Action<HttpClient>? configureClient)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("A Groq API key is required.", nameof(apiKey));

            return services.AddGroqApiClient(_ => apiKey, configureClient);
        }

        /// <summary>
        /// Registers <see cref="IGroqApiClient"/> as above, resolving the Groq API key from the
        /// <see cref="IServiceProvider"/> at registration time via <paramref name="apiKeyFactory"/>
        /// (for example reading it from <c>IConfiguration</c> without capturing a raw key string in
        /// calling code).
        /// </summary>
        /// <param name="services">The service collection to add the client to.</param>
        /// <param name="apiKeyFactory">Factory that produces the Groq API key from the service provider.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for further configuration.</returns>
        public static IHttpClientBuilder AddGroqApiClient(
            this IServiceCollection services,
            Func<IServiceProvider, string> apiKeyFactory)
            => services.AddGroqApiClient(apiKeyFactory, configureClient: null);

        /// <summary>
        /// Registers <see cref="IGroqApiClient"/> as above, resolving the API key from the service
        /// provider at registration time and exposing a hook to customize the managed
        /// <see cref="HttpClient"/>. The configuration runs before the client's auth header is applied,
        /// so callers should not overwrite the <c>Authorization</c> header here.
        /// </summary>
        /// <param name="services">The service collection to add the client to.</param>
        /// <param name="apiKeyFactory">Factory that produces the Groq API key from the service provider.</param>
        /// <param name="configureClient">Optional callback to configure the managed <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> for further configuration.</returns>
        public static IHttpClientBuilder AddGroqApiClient(
            this IServiceCollection services,
            Func<IServiceProvider, string> apiKeyFactory,
            Action<HttpClient>? configureClient)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (apiKeyFactory is null) throw new ArgumentNullException(nameof(apiKeyFactory));

            return services.AddHttpClient<IGroqApiClient, GroqApiClient>((httpClient, sp) =>
            {
                configureClient?.Invoke(httpClient);
                return new GroqApiClient(apiKeyFactory(sp), httpClient);
            });
        }
    }
}
