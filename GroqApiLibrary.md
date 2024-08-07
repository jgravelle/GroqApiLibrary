# GroqApiLibrary Solution Code Files

## GroqApiClient.cs

```
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public class GroqApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.groq.com/openai/v1";
        private const string ChatCompletionsEndpoint = "/chat/completions";
        private const string TranscriptionsEndpoint = "/audio/transcriptions";
        private const string TranslationsEndpoint = "/audio/translations";

        public GroqApiClient(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<JsonObject?> CreateChatCompletionAsync(JsonObject request)
        {
            var response = await _httpClient.PostAsJsonAsync(BaseUrl + ChatCompletionsEndpoint, request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}. Response content: {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public async IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request)
        {
            request["stream"] = true;
            var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl + ChatCompletionsEndpoint) { Content = content };
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("data: "))
                {
                    var data = line["data: ".Length..];
                    if (data != "[DONE]")
                    {
                        yield return JsonSerializer.Deserialize<JsonObject>(data);
                    }
                }
            }
        }

        public async Task<JsonObject?> CreateTranscriptionAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", string? language = null, float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(audioFile), "file", fileName);
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (!string.IsNullOrEmpty(language))
                content.Add(new StringContent(language), "language");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString()), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranscriptionsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public async Task<JsonObject?> CreateTranslationAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", float? temperature = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(audioFile), "file", fileName);
            content.Add(new StringContent(model), "model");

            if (!string.IsNullOrEmpty(prompt))
                content.Add(new StringContent(prompt), "prompt");

            content.Add(new StringContent(responseFormat), "response_format");

            if (temperature.HasValue)
                content.Add(new StringContent(temperature.Value.ToString()), "temperature");

            var response = await _httpClient.PostAsync(BaseUrl + TranslationsEndpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonObject>();
        }

        public async Task<string> RunConversationWithToolsAsync(string userPrompt, List<Tool> tools, string model, string systemMessage)
        {
            try
            {
                var messages = new List<JsonObject>
                {
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = systemMessage
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = userPrompt
                    }
                };

                var request = new JsonObject
                {
                    ["model"] = model,
                    ["messages"] = JsonSerializer.SerializeToNode(messages),
                    ["tools"] = JsonSerializer.SerializeToNode(tools.Select(t => new
                    {
                        type = t.Type,
                        function = new
                        {
                            name = t.Function.Name,
                            description = t.Function.Description,
                            parameters = t.Function.Parameters
                        }
                    })),
                    ["tool_choice"] = "auto"
                };

                Console.WriteLine($"Sending request to API: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

                var response = await CreateChatCompletionAsync(request);
                var responseMessage = response?["choices"]?[0]?["message"]?.AsObject();
                var toolCalls = responseMessage?["tool_calls"]?.AsArray();

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    messages.Add(responseMessage);
                    foreach (var toolCall in toolCalls)
                    {
                        var functionName = toolCall?["function"]?["name"]?.GetValue<string>();
                        var functionArgs = toolCall?["function"]?["arguments"]?.GetValue<string>();
                        var toolCallId = toolCall?["id"]?.GetValue<string>();

                        if (!string.IsNullOrEmpty(functionName) && !string.IsNullOrEmpty(functionArgs))
                        {
                            var tool = tools.Find(t => t.Function.Name == functionName);
                            if (tool != null)
                            {
                                var functionResponse = await tool.Function.ExecuteAsync(functionArgs);
                                messages.Add(new JsonObject
                                {
                                    ["tool_call_id"] = toolCallId,
                                    ["role"] = "tool",
                                    ["name"] = functionName,
                                    ["content"] = functionResponse
                                });
                            }
                        }
                    }

                    request["messages"] = JsonSerializer.SerializeToNode(messages);
                    var secondResponse = await CreateChatCompletionAsync(request);
                    return secondResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
                }

                return responseMessage?["content"]?.GetValue<string>() ?? string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request error: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }


    public class Tool
    {
        public string Type { get; set; } = "function";
        public Function Function { get; set; }
    }

    public class Function
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JsonObject Parameters { get; set; }
        public Func<string, Task<string>> ExecuteAsync { get; set; }
    }
}
```

## GroqLlmProvider.cs

```
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;

namespace GroqApiLibrary
{
    public interface ILlmProvider
    {
        Task<string> GenerateAsync(string prompt);
    }


    public class GroqLlmProvider : ILlmProvider, IDisposable
    {
        private readonly GroqApiClient _client;
        private readonly string _model;

        public GroqLlmProvider(string apiKey, string model)
        {
            _client = new GroqApiClient(apiKey);
            _model = model;
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            var request = new JsonObject
            {
                ["model"] = _model,
                ["messages"] = JsonSerializer.SerializeToNode(new[]
                {
                new { role = "user", content = prompt }
            })
            };

            var response = await _client.CreateChatCompletionAsync(request);
            return response?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}

```

## IGroqApiClient.cs

```

using System.Text.Json.Nodes;

namespace GroqApiLibrary
{
    public interface IGroqApiClient
    {
        Task<JsonObject?> CreateChatCompletionAsync(JsonObject request);
        IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request);
        Task<JsonObject?> CreateTranscriptionAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", string? language = null, float? temperature = null);
        Task<JsonObject?> CreateTranslationAsync(Stream audioFile, string fileName, string model,
            string? prompt = null, string responseFormat = "json", float? temperature = null);
        Task<string> RunConversationWithToolsAsync(string userPrompt, List<Tool> tools, string model, string systemMessage);
    }
}
```

## C:\DLLs\GroqToolLibrary\BaseTool.cs

```
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GroqToolLibrary
{
    public abstract class BaseTool
    {
        public abstract Task<object> ExecuteAsync(params object[] args);

        public virtual string? ValidateInput(IDictionary<string, object> data)
        {
            return null;
        }

        public virtual IDictionary<string, object> FormatOutput(object result)
        {
            return new Dictionary<string, object> { { "result", result } };
        }

        public virtual string HandleError(Exception error)  
        {
            return $"An error occurred: {error.Message}";
        }
    }
}
```

## C:\DLLs\GroqToolLibrary\WebTools\WebContentTool.cs

```
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System.Text;

namespace GroqToolLibrary.WebTools
{
    public class WebContentTool : BaseTool
    {
        private readonly HttpClient _httpClient;
        private readonly bool _debug;

        public WebContentTool(bool debug = false)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _debug = debug;
        }

        public override async Task<object> ExecuteAsync(params object[] args)
        {
            if (args.Length != 1 || !(args[0] is string url))
                throw new ArgumentException("Invalid argument. Expected a single URL string.");

            return await WebGetContentsAsync(url);
        }

        private async Task<string> WebGetContentsAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(content);

                // Remove script and style elements
                htmlDocument.DocumentNode.Descendants()
                    .Where(n => n.Name == "script" || n.Name == "style")
                    .ToList()
                    .ForEach(n => n.Remove());

                var text = htmlDocument.DocumentNode.InnerText;

                // Process the text similar to the Python version
                var lines = text.Split('\n').Select(line => line.Trim());
                var chunks = lines.SelectMany(line => line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)).Select(chunk => chunk.Trim());
                var processedText = string.Join("\n", chunks.Where(chunk => !string.IsNullOrWhiteSpace(chunk)));

                if (_debug)
                {
                    Console.WriteLine($"Successfully retrieved content from {url}");
                    Console.WriteLine($"Content preview: {processedText.Substring(0, Math.Min(4000, processedText.Length))}...");
                }

                return processedText;
            }
            catch (HttpRequestException e)
            {
                var errorMessage = $"Error retrieving content from {url}: {e.Message}";
                if (_debug)
                {
                    Console.WriteLine(errorMessage);
                }
                return null;
            }
        }

        public override string? ValidateInput(IDictionary<string, object> data)
        {
            if (!data.ContainsKey("url") || !(data["url"] is string))
                return "Missing or invalid 'url' parameter";

            return null;
        }

        public override IDictionary<string, object> FormatOutput(object result)
        {
            if (result is string content)
            {
                return new Dictionary<string, object>
                {
                    { "content", content }
                };
            }
            else
            {
                return new Dictionary<string, object>
                {
                    { "error", "Failed to retrieve content" }
                };
            }
        }
    }
}
```

## C:\DLLs\GroqToolLibrary\WebTools\WebGetLinksTool.cs

```
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;

namespace GroqToolLibrary.WebTools
{
    public class WebGetLinksTool : BaseTool
    {
        private readonly HttpClient _httpClient;
        private readonly bool _debug;

        public WebGetLinksTool(bool debug = false)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _debug = debug;
        }

        public override async Task<object> ExecuteAsync(params object[] args)
        {
            if (args.Length != 1 || !(args[0] is string url))
                throw new ArgumentException("Invalid argument. Expected a single URL string.");

            return await WebGetLinksAsync(url);
        }

        private async Task<object> WebGetLinksAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(content);

                var links = htmlDocument.DocumentNode.SelectNodes("//a[@href]")
                    .Select(a => new
                    {
                        Text = a.InnerText.Trim(),  
                        Target = a.GetAttributeValue("href", "")
                    })
                    .ToList();

                if (_debug)
                {
                    Console.WriteLine($"Found {links.Count} links on the page");
                    foreach (var link in links)
                    {
                        Console.WriteLine($"Text: {link.Text}");
                        Console.WriteLine($"Target: {link.Target}");
                    }
                }

                return links;
            }
            catch (HttpRequestException e)
            {
                return $"An error occurred: {e.Message}";
            }
        }

        public override string? ValidateInput(IDictionary<string, object> data)
        {
            if (!data.ContainsKey("url") || !(data["url"] is string))
                return "Missing or invalid 'url' parameter";

            return null;
        }

        public override IDictionary<string, object> FormatOutput(object result)
        {
            if (result is List<object> links)
            {
                return new Dictionary<string, object>
                {
                    { "links", links }
                };
            }
            else if (result is string errorMessage)
            {
                return new Dictionary<string, object>
                {
                    { "error", errorMessage }
                };
            }
            return base.FormatOutput(result);
        }
    }
}
```

## C:\DLLs\GroqToolLibrary\WebTools\WebSearchTool.cs

```
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Linq;

namespace GroqToolLibrary.WebTools
{
    public class WebSearchTool : BaseTool, IDisposable
    {
        private readonly IWebDriver _driver;

        public WebSearchTool()
        {
            var options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("disable-gpu");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-dev-shm-usage");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            _driver = new ChromeDriver(options);
        }

        public override async Task<object> ExecuteAsync(params object[] args)
        {
            if (args.Length < 1 || args.Length > 2)
                throw new ArgumentException("Invalid number of arguments. Expected: query, [numResults]");

            string query = args[0].ToString();
            int numResults = args.Length > 1 ? Convert.ToInt32(args[1]) : 10;

            return await Task.Run(() => WebSearch(query, numResults));
        }

        private List<Dictionary<string, string>> WebSearch(string query, int numResults)
        {
            string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}&num={numResults}";

            _driver.Navigate().GoToUrl(url);

            // Wait for the search results to load
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            var results = new List<Dictionary<string, string>>();

            var searchResults = _driver.FindElements(By.CssSelector("div.g"));

            foreach (var result in searchResults.Take(numResults))  
            {
                var item = new Dictionary<string, string>();

                try
                {
                    var titleElement = result.FindElement(By.CssSelector("h3"));
                    item["title"] = titleElement.Text;

                    var linkElement = result.FindElement(By.CssSelector("a"));
                    item["url"] = linkElement.GetAttribute("href");

                    var descElement = result.FindElement(By.CssSelector(".VwiC3b"));
                    item["description"] = descElement.Text;

                    results.Add(item);
                }
                catch (NoSuchElementException)
                {
                    // Skip this result if we can't find all required elements
                    continue;
                }
            }

            return results;
        }

        public override string? ValidateInput(IDictionary<string, object> data)
        {
            if (!data.ContainsKey("query"))
                return "Missing 'query' parameter";

            if (data.ContainsKey("numResults") && (!int.TryParse(data["numResults"].ToString(), out int numResults) || numResults <= 0))
                return "Invalid 'numResults' parameter. Must be a positive integer";

            return null;
        }

        public override IDictionary<string, object> FormatOutput(object result)
        {
            if (result is List<Dictionary<string, string>> searchResults)
            {
                return new Dictionary<string, object>
                {
                    { "results", searchResults }
                };
            }
            return base.FormatOutput(result);
        }

        public void Dispose()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
```

## C:\DLLs\GroqToolsCLI\Program.cs

```
using System;
using System.Threading.Tasks;
using GroqToolLibrary.WebTools;
using System.Text.Json;

namespace GroqToolsCLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome to the GroqTools CLI!");

            while (true)
            {
                DisplayMenu();
                string choice = Console.ReadLine().Trim();

                switch (choice)
                {
                    case "1":
                        await PerformWebSearch();
                        break;
                    case "2":
                        await PerformWebGetLinks();
                        break;
                    case "3":
                        await PerformWebGetContents();
                        break;
                    case "4":
                        Console.WriteLine("Thank you for using GroqTools CLI. Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        static void DisplayMenu()
        {
            Console.WriteLine("\nPlease select an option:");
            Console.WriteLine("1. Perform a web search");
            Console.WriteLine("2. Get links from a webpage");
            Console.WriteLine("3. Get contents of a webpage");
            Console.WriteLine("4. Exit");
            Console.Write("Enter your choice (1-4): ");
        }

        static async Task PerformWebSearch()
        {
            Console.Write("Enter your search query: ");
            string query = Console.ReadLine();

            int numResults = GetValidNumber("Enter the number of results you want (1-50): ", 1, 50);

            using (var webSearchTool = new WebSearchTool())
            {
                var results = await webSearchTool.ExecuteAsync(query, numResults);
                DisplayResults(results);
            }
        }

        static async Task PerformWebGetContents()
        {
            Console.Write("Enter the URL to retrieve contents from: ");
            string url = Console.ReadLine();

            var webContentTool = new WebContentTool(debug: true);
            var results = await webContentTool.ExecuteAsync(url);
            DisplayResults(results);
        }

        static async Task PerformWebGetLinks()
        {
            Console.Write("Enter the URL to extract links from: ");
            string url = Console.ReadLine();

            var webGetLinksTool = new WebGetLinksTool(debug: true);
            var results = await webGetLinksTool.ExecuteAsync(url);
            DisplayResults(results);
        }

        static void DisplayResults(object results)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("\nResults:");
            Console.WriteLine(JsonSerializer.Serialize(results, options));

            if (results is string content)
            {
                Console.WriteLine("\nContent Preview (first 2000 characters):");
                Console.WriteLine(content.Substring(0, Math.Min(2000, content.Length)));
            }
        }

        static int GetValidNumber(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int number) && number >= min && number <= max)
                {
                    return number;
                }
                Console.WriteLine($"Invalid input. Please enter a number between {min} and {max}.");
            }
        }
    }
}
```

## C:\DLLs\GroqToolsCLI\Properties\launchSettings.json

```
{
  "profiles": {
    "GroqToolsCLI": {
      "commandName": "Project"
    }
  }
}
```

## C:\DLLs\GroqToolsCLI\Properties\Resources.Designer.cs

```
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GroqToolsCLI.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GroqToolsCLI.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
    }
}

```

## C:\DLLs\GroqAgentLibrary\BaseAgent.cs

```
using GroqApiLibrary;

namespace GroqAgentLibrary
{
    public abstract class BaseAgent
    {
        protected readonly ILlmProvider Provider;
        protected readonly string Model;

        protected BaseAgent(ILlmProvider provider, string model)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public abstract Task<object> ProcessRequestAsync(string request);

        protected virtual string CreateSummaryPrompt(string content, string userRequest)
        {
            return @$"
            Given the following content:
            {content}
            Respond to the user's request: ""{userRequest}""

            Provide a concise and relevant summary that directly addresses the user's request.
            ";
        }

        protected virtual async Task<string> SummarizeContentAsync(string content, string userRequest)
        {
            string summaryPrompt = CreateSummaryPrompt(content, userRequest);
            return await Provider.GenerateAsync(summaryPrompt);
        }

        protected virtual string FormatResponse(IDictionary<string, object> data)
        {
            return string.Join("\n", data.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }
    }
}
```

## C:\DLLs\GroqAgentLibrary\WebAgent.cs

```
using GroqApiLibrary;
using GroqToolLibrary.WebTools;

namespace GroqAgentLibrary
{
    public class WebAgent : BaseAgent
    {
        private readonly List<string> _skipDomains = new List<string>
        {
            "reddit.com",
            // Add more domains to skip here
        };

        private readonly WebSearchTool _webSearchTool;
        private readonly WebGetLinksTool _webGetLinksTool;
        private readonly WebContentTool _webContentTool;
        private readonly bool _debug;

        public WebAgent(ILlmProvider llmProvider, string model, bool debug = false)
            : base(llmProvider, model)
        {
            _webSearchTool = new WebSearchTool();
            _webGetLinksTool = new WebGetLinksTool();
            _webContentTool = new WebContentTool();
            _debug = debug;
        }

        public override async Task<object> ProcessRequestAsync(string request)
        {
            try
            {
                return await ProcessWebSearchAsync(request);
            }
            catch (Exception e)
            {
                LogDebug($"Error in WebAgent: {e.Message}");
                return $"An error occurred while processing your request: {e.Message}";
            }
        }

        private async Task<string> ProcessWebSearchAsync(string userRequest)
        {
            var searchResults = await PerformWebSearchAsync(userRequest);
            if (!searchResults.Any())
            {
                return "I'm sorry, but I couldn't find any relevant information for your request.";
            }

            var filteredResults = FilterSearchResults(searchResults);
            if (!filteredResults.Any())
            {
                return "I found some results, but they were all from domains I've been instructed to skip. Could you try rephrasing your request?";
            }

            var allSummaries = new List<string>();
            foreach (var result in filteredResults)
            {
                LogDebug($"Trying URL: {result.Url}");
                var content = await GetWebContentAsync(result.Url);
                if (!string.IsNullOrEmpty(content))
                {
                    var summary = await SummarizeWebContentAsync(content, userRequest, result.Url, result.Description);
                    allSummaries.Add(summary);
                    if (summary.Contains("TERMINATE"))
                    {
                        LogDebug($"Satisfactory answer found at {result.Url}");
                        return summary.Replace("TERMINATE", "").Trim();
                    }
                }
                LogDebug("Moving to next URL as current one did not provide a satisfactory answer.");
            }

            return allSummaries.Any()
                ? await CombineSummariesAsync(allSummaries, userRequest)
                : "I'm sorry, but I couldn't find satisfactory information to answer your request after checking multiple sources.";
        }

        private async Task<List<SearchResult>> PerformWebSearchAsync(string query)
        {
            try
            {
                var results = await _webSearchTool.ExecuteAsync(query, 5) as List<SearchResult>;
                LogDebug($"WebSearchTool found {results?.Count ?? 0} results for the query: {query}");
                LogDebug($"First result: {results?.FirstOrDefault()}");
                return results ?? new List<SearchResult>();
            }
            catch (Exception e)
            {
                LogDebug($"Error in WebSearchTool: {e.Message}");
                return new List<SearchResult>();
            }
        }

        private List<SearchResult> FilterSearchResults(List<SearchResult> results)
        {
            var filteredResults = results.Where(result => !_skipDomains.Any(domain => result.Url.Contains(domain))).ToList();
            LogDebug($"Filtered out {results.Count - filteredResults.Count} URLs");
            LogDebug($"Remaining URLs: {string.Join(", ", filteredResults.Select(r => r.Url))}");
            return filteredResults;
        }

        private async Task<string> GetWebContentAsync(string url)
        {
            try
            {
                var content = await _webContentTool.ExecuteAsync(url) as string;
                LogDebug($"Successfully retrieved content from {url}");
                LogDebug($"Content preview: {content?.Substring(0, Math.Min(200, content?.Length ?? 0))}...");
                return content ?? string.Empty;
            }
            catch (Exception e)
            {
                LogDebug($"Error retrieving content from {url}: {e.Message}");
                return string.Empty;
            }
        }

        private async Task<string> SummarizeWebContentAsync(string content, string userRequest, string url, string description)
        {
            var summaryPrompt = CreateSummaryPrompt(content, userRequest, url, description);
            return await Provider.GenerateAsync(summaryPrompt);
        }

        protected override string CreateSummaryPrompt(string content, string userRequest)
        {
            throw new InvalidOperationException("Use the overload with URL and description for WebAgent");
        }

        private string CreateSummaryPrompt(string content, string userRequest, string url, string description)
        {
            return @$"
            Given the following web content from {url}:
            Description: {description}
            Content: {content.Substring(0, Math.Min(2000, content.Length))}

            Respond to the user's request: ""{userRequest}""

            Rules:
            1. Provide ONLY the specific information requested. Do not include any additional context or explanations.
            2. If the exact information is not available, provide the closest relevant information without elaboration.
            3. Use simple, direct language. Avoid introductory phrases or unnecessary words.
            4. If the information is not relevant to the user's request, respond with ONLY: ""No relevant information found.""
            5. If you've provided a complete and satisfactory answer, append 'TERMINATE' to the end of your response.
            6. If the information doesn't completely answer the user's request, do not append 'TERMINATE'.
            ";
        }

        private async Task<string> CombineSummariesAsync(List<string> summaries, string userRequest)
        {
            var combinedPrompt = @$"
            Given the following summaries from multiple sources:
            {string.Join(" ", summaries)}

            Respond to the user's request: ""{userRequest}""

            Rules:
            1. Provide ONLY the specific information requested. Do not include any additional context, explanations, or narrative.
            2. Use simple, direct language. Avoid introductory phrases or unnecessary words.
            3. If the summaries contain conflicting information, provide only the most reliable information without mentioning the conflict.
            4. If the user's request is not fully answered, provide only the available information without mentioning what's missing.
            5. Your response should be a single, concise sentence or a brief list of facts, whichever is more appropriate for the requested information.
            6. Do not include any statements about the source of the information or the process of finding it.
            ";
            return await Provider.GenerateAsync(combinedPrompt);
        }

        private void LogDebug(string message)
        {
            if (_debug)
            {
                Console.WriteLine($"[DEBUG] {message}");
            }
        }
    }

    public class SearchResult
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }

        public override string ToString() => $"{{Title: {Title}, Url: {Url}, Description: {Description}}}";
    }
}
```

## C:\DLLs\GroqAgentsCLI\Program.cs

```
using GroqAgentLibrary;
using GroqApiLibrary;
using System;
using System.Threading.Tasks;

namespace GroqAgentsCLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome to the GroqAgents CLI!");

            string apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("WARNING: GROQ_API_KEY environment variable not found. Using fallback method.");
                apiKey = "your-fallback-api-key-here";  // Only as a last resort
            }

            string model = "llama-3.1-8b-instant";  // You might want to make this configurable too

            ILlmProvider llmProvider = new GroqLlmProvider(apiKey, model);

            var webAgent = new WebAgent(llmProvider, model, debug: true);

            while (true)
            {
                DisplayMenu();
                string choice = Console.ReadLine().Trim();

                switch (choice)
                {
                    case "1":
                        await InteractWithWebAgent(webAgent);
                        break;
                    case "2":
                        Console.WriteLine("Thank you for using GroqAgents CLI. Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        static void DisplayMenu()
        {
            Console.WriteLine("\nPlease select an agent to interact with:");
            Console.WriteLine("1. Web Agent");
            Console.WriteLine("2. Exit");
            Console.Write("Enter your choice (1-2): ");
        }

        static async Task InteractWithWebAgent(WebAgent webAgent)
        {
            while (true)
            {
                Console.Write("\nWhat would you like to know? (Type 'back' to return to main menu): ");
                string userRequest = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userRequest) || userRequest.ToLower() == "back")
                {
                    break;
                }

                object response = await webAgent.ProcessRequestAsync(userRequest);
                Console.WriteLine(response.ToString());
            }
        }
    }

    // You need to implement this class
    public class YourLlmProviderImplementation : ILlmProvider
    {
        public Task<string> GenerateAsync(string prompt)
        {
            // Implement your LLM provider logic here
            throw new NotImplementedException();
        }
    }
}
```

