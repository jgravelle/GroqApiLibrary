using Microsoft.AspNetCore.Components;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public class LlmCommunicationService : ILlmCommunicationService
    {
        private readonly GroqApiClient _client;
        private readonly string _apiKey = "xxxxx"; 
        private readonly string _model = "mixtral-8x7b-32768"; // mixtral-8x7b-32768 llama2-70b-4096

        public LlmCommunicationService()
        {
            _client = new GroqApiClient(_apiKey, _model);
        }

        public async Task<string> GetResponseAsync(string input)
        {
            try
            {
                // Assuming GroqApiClient has a method that actually sends a request and returns a response string
                string? responseContent = await _client.SendRequestAndGetResponseAsync(input);
                if (responseContent == null)
                {
                    Console.WriteLine("Received a null response from the API.");
                    return "Error: API returned null response.";
                }

                // Log the raw response string for debugging
                Console.WriteLine($"Raw response: {responseContent}");

                // You can add additional logic here to check if the responseContent is valid JSON

                return responseContent; // Return the response content directly
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Oops, encountered a network issue: {ex.Message}");
                return "Error: Network issue";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected issue occurred: {ex.Message}");
                return "Error: Unexpected issue";
            }
        }
    }
}