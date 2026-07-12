using Microsoft.Extensions.DependencyInjection;

namespace GroqApiLibrary
{
    /// <summary>
    /// DI registration helpers for <see cref="IGroqApiClient"/>. Lives in its own file so
    /// consumers who don't use dependency injection aren't forced to reference
    /// Microsoft.Extensions.Http/DependencyInjection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        private const string HttpClientName = "GroqApiClient";

        /// <summary>
        /// Registers <see cref="IGroqApiClient"/> backed by a named <see cref="HttpClient"/> managed
        /// through <see cref="IHttpClientFactory"/> (avoiding socket exhaustion from manually-created
        /// clients).
        /// </summary>
        public static IServiceCollection AddGroqApiClient(this IServiceCollection services, string apiKey)
        {
            return services.AddGroqApiClient(_ => apiKey);
        }

        /// <summary>
        /// Registers <see cref="IGroqApiClient"/>, resolving the API key from configuration/environment
        /// at registration time via <paramref name="apiKeyFactory"/> (e.g. reading it from
        /// <c>IConfiguration</c> without capturing a raw key string in calling code).
        /// </summary>
        public static IServiceCollection AddGroqApiClient(this IServiceCollection services, Func<IServiceProvider, string> apiKeyFactory)
        {
            services.AddHttpClient(HttpClientName);
            services.AddTransient<IGroqApiClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
                return new GroqApiClient(apiKeyFactory(sp), httpClient);
            });
            return services;
        }
    }
}
