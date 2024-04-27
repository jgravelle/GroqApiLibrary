using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        Task<JObject> CreateChatCompletionAsync(JObject request);

        IAsyncEnumerable<JObject> CreateChatCompletionStreamAsync(JObject request);
    }
}