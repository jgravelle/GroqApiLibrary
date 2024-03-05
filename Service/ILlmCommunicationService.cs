using Microsoft.AspNetCore.Components;

namespace GroqApiLibrary
{
    public interface ILlmCommunicationService
    {
        Task<string> GetResponseAsync(string input);

    }
}