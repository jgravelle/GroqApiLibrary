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

        public async Task<JsonObject?> ListModelsAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{BaseUrl}/models");
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject? responseJson = JsonSerializer.Deserialize<JsonObject>(responseString);

            return responseJson;
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
        Task<JsonObject?> ListModelsAsync();
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
            // Assume 'query' contains the user's search query
            string sanitizedQuery = query.Trim('"', '\''); // Remove leading and trailing quotes

            // Construct the URL
            string url = $"https://www.google.com/search?q={Uri.EscapeDataString(sanitizedQuery)}&num={numResults}";


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

## C:\DLLs\GroqToolLibrary\WebTools\WebWeatherTool.cs

```
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace GroqToolLibrary.WebTools
{
    public class WebWeatherTool : BaseTool
    {
        private const string BASE_URL = "https://weathermateplus.com/api/location/";
        private static readonly HttpClient client = new HttpClient();

        public override async Task<object> ExecuteAsync(params object[] args)
        {
            if (args.Length != 1 || !(args[0] is string address))
                throw new ArgumentException("Invalid argument. Expected a single string address.");

            try
            {
                return await FetchWeatherAsync(address);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.BadGateway
                                              || e.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = $"The weather service is temporarily unavailable. Please try again later. (Status: {e.StatusCode})"
                };
            }
            catch (Exception e)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = $"An error occurred while fetching weather data: {e.Message}"
                };
            }
        }

        private async Task<Dictionary<string, object>> FetchWeatherAsync(string address)
        {
            try
            {
                string encodedAddress = HttpUtility.UrlEncode(address);
                string url = $"{BASE_URL}?address={encodedAddress}";

                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JsonDocument data = JsonDocument.Parse(responseBody);
                return FormatOutput(ExtractRelevantData(data.RootElement));
            }
            catch (HttpRequestException e)
            {
                return HandleError($"Error fetching weather data: {e.Message}");
            }
            catch (JsonException e)
            {
                return HandleError($"Error parsing weather data: {e.Message}");
            }
        }

        private Dictionary<string, object> ExtractRelevantData(JsonElement data)
        {
            return new Dictionary<string, object>
            {
                ["location"] = data.GetProperty("location"),
                ["currentObservation"] = data.GetProperty("currentObservation"),
                ["day1"] = data.GetProperty("days").GetArrayLength() > 0 ? data.GetProperty("days")[0] : null
            };
        }

        private Dictionary<string, object> FormatOutput(Dictionary<string, object> result)
        {
            try
            {
                var formatted = new Dictionary<string, object>
                {
                    ["location"] = GetPropertyAsString(result["location"], "areaDescription"),
                    ["current"] = new Dictionary<string, object>
                    {
                        ["temperature"] = GetPropertyAsString(result["currentObservation"], "temperature"),
                        ["weather"] = GetPropertyAsString(result["currentObservation"], "weather"),
                        ["windSpeed"] = GetPropertyAsString(result["currentObservation"], "windSpeed"),
                        ["windDirection"] = GetPropertyAsString(result["currentObservation"], "windDirection")
                    },
                    ["forecast"] = new Dictionary<string, object>()
                };

                if (result["day1"] != null)
                {
                    formatted["forecast"] = new Dictionary<string, object>
                    {
                        ["temperature"] = GetPropertyAsString(result["day1"], "temperature"),
                        ["shortForecast"] = GetPropertyAsString(result["day1"], "shortForecast"),
                        ["windSpeed"] = GetPropertyAsString(result["day1"], "windSpeed"),
                        ["windDirection"] = GetPropertyAsString(result["day1"], "windDirection"),
                        ["precipitationProbability"] = GetPropertyAsString(result["day1"], "probabilityOfPrecipitation")
                    };
                }

                return formatted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FormatOutput: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return new Dictionary<string, object> { ["error"] = $"Error formatting weather data: {ex.Message}" };
            }
        }

        private string GetPropertyAsString(object obj, string propertyName)
        {
            try
            {
                if (obj is JsonElement element)
                {
                    if (element.TryGetProperty(propertyName, out JsonElement property))
                    {
                        return property.ValueKind switch
                        {
                            JsonValueKind.String => property.GetString(),
                            JsonValueKind.Number => property.GetDouble().ToString(),
                            _ => property.ToString()
                        };
                    }
                    else
                    {
                        Console.WriteLine($"Property {propertyName} not found in JsonElement");
                        return string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine($"Object is not a JsonElement. Type: {obj?.GetType().Name ?? "null"}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPropertyAsString for property {propertyName}: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetValueAsString(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetDouble().ToString();
                default:
                    return element.ToString();
            }
        }

        private Dictionary<string, object> HandleError(string errorMessage)
        {
            Console.WriteLine($"WebWeatherTool error: {errorMessage}");

            return new Dictionary<string, object>
            {
                ["error"] = errorMessage,
                ["status"] = "error"
            };
        }

        public override string ValidateInput(IDictionary<string, object> data)
        {
            if (!data.ContainsKey("address"))
                return "Address is required.";
            if (!(data["address"] is string))
                return "Address must be a string.";
            return null;
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

## C:\DLLs\GroqAgentLibrary\BossAgent.cs

```
using GroqAgentLibrary;
using GroqApiLibrary;

public class BossAgent : BaseAgent
{
    private readonly WebAgent _webAgent;
    private readonly bool _debug;
    private const int MAX_DEFER_ATTEMPTS = 5;

    public BossAgent(ILlmProvider llmProvider, string model, bool debug = false)
        : base(llmProvider, model)
    {
        _webAgent = new WebAgent(llmProvider, model, debug);
        _debug = debug;
    }

    private async Task<(bool IsSimpleQuery, string Reasoning)> AnalyzeRequestAsync(string request)
    {
        var prompt = $@"Analyze the following request:

            '{request}'

            Determine if this is a simple, single-task query or a complex, multi-task query.

            Respond with:
            1. Either 'SIMPLE' or 'COMPLEX' indicating whether this is a simple or complex query.
            2. A brief explanation of your reasoning.

            Format your response as: SIMPLE/COMPLEX: Reasoning";

        var response = await Provider.GenerateAsync(prompt);
        var parts = response.Split(new[] { ':' }, 2);

        if (parts.Length != 2)
        {
            throw new FormatException("Unexpected response format from LLM during request analysis.");
        }

        return (parts[0].Trim().ToUpper() == "SIMPLE", parts[1].Trim());
    }

    private async Task<object> HandleComplexQueryAsync(string request)
    {
        var tasks = await BreakdownRequestAsync(request);
        var responses = new List<string>();

        foreach (var task in tasks)
        {
            var taskResponse = await HandleSimpleQueryAsync(task);
            responses.Add($"Task: {task}\nResponse: {taskResponse}\n");
        }

        return CombineResponses(responses);
    }

    private async Task<object> HandleQueryAsync(string request, int deferCount)
    {
        if (deferCount >= MAX_DEFER_ATTEMPTS)
        {
            return "I apologize, but I'm having trouble finding the information you requested. Could you please rephrase your question or ask about something else?";
        }

        var analysisResult = await AnalyzeTaskAsync(request);

        if (analysisResult.CanHandleDirect)
        {
            return await GenerateDirectResponseAsync(request, analysisResult.Reasoning);
        }
        else
        {
            LogDebug($"Deferring to WebAgent (Attempt {deferCount + 1}). Reasoning: {analysisResult.Reasoning}");
            var webAgentResponse = await _webAgent.ProcessRequestAsync(request);
            var reviewResult = await ReviewWebAgentResponseAsync(request, webAgentResponse.ToString());

            if (reviewResult.NeedsFollowUp)
            {
                return await HandleQueryAsync(reviewResult.FollowUpQuery, deferCount + 1);
            }
            else
            {
                return reviewResult.Response;
            }
        }
    }

    private async Task<object> HandleSimpleQueryAsync(string request)
    {
        var analysisResult = await AnalyzeTaskAsync(request);

        if (analysisResult.CanHandleDirect)
        {
            return await GenerateDirectResponseAsync(request, analysisResult.Reasoning);
        }
        else
        {
            LogDebug($"Deferring to WebAgent. Reasoning: {analysisResult.Reasoning}");
            var webAgentResponse = await _webAgent.ProcessRequestAsync(request);
            return await ReviewWebAgentResponseAsync(request, webAgentResponse.ToString());
        }
    }

    private async Task<List<string>> BreakdownRequestAsync(string request)
    {
        var prompt = $@"Break down the following request into individual tasks:

            '{request}'

            Each task should be a separate, actionable item. Return the tasks as a numbered list, with each task on a new line. Do not include any additional text or explanations.";

        var response = await Provider.GenerateAsync(prompt);
        return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(line => line.TrimStart('1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ' '))
                       .ToList();
    }

    private async Task<(bool CanHandleDirect, string Reasoning)> AnalyzeTaskAsync(string task)
    {
        var prompt = $@"Analyze the following task:

            '{task}'

            Determine if you can confidently and accurately complete this task based solely on your current knowledge, without needing to search for additional information.

            Respond with:
            1. Either 'YES' or 'NO' indicating whether you can handle this task directly.
            2. A brief explanation of your reasoning.

            Format your response as: YES/NO: Reasoning";

        var response = await Provider.GenerateAsync(prompt);
        var parts = response.Split(new[] { ':' }, 2);

        if (parts.Length != 2)
        {
            throw new FormatException("Unexpected response format from LLM during task analysis.");
        }

        return (parts[0].Trim().ToUpper() == "YES", parts[1].Trim());
    }

    private async Task<string> GenerateDirectResponseAsync(string task, string reasoning)
    {
        LogDebug($"Generating direct response for task: {task}. Reasoning: {reasoning}");

        var prompt = $@"Based on the task:

            '{task}'

            And your reasoning that you can handle this directly:

            '{reasoning}'

            Please provide a comprehensive, accurate, and helpful response to complete this task.";

        return await Provider.GenerateAsync(prompt);
    }

    public override async Task<object> ProcessRequestAsync(string request)
    {
        try
        {
            return await HandleQueryAsync(request, 0);
        }
        catch (Exception e)
        {
            LogDebug($"Error in BossAgent: {e.Message}");
            return $"I apologize, but an error occurred while processing your request: {e.Message}";
        }
    }

    private async Task<(bool NeedsFollowUp, string Response, string FollowUpQuery)> ReviewWebAgentResponseAsync(string originalTask, string webAgentResponse)
    {
        var prompt = $@"You are reviewing a response generated by another AI agent to the following task:

Original Task: '{originalTask}'

WebAgent's Response:
{webAgentResponse}

Your task is to review this response and determine if it adequately addresses the original task. 

If it does, return the response prefixed with 'SUFFICIENT:'. 

If it partially addresses the task but needs more information, return the response prefixed with 'PARTIAL:' and include a follow-up question to get the missing information.

If it doesn't address the task at all, return 'INSUFFICIENT:' followed by a rephrased version of the original task.

Ensure the final response is comprehensive, accurate, and directly addresses the original task.";

        var reviewResult = await Provider.GenerateAsync(prompt);

        if (reviewResult.StartsWith("SUFFICIENT:"))
        {
            return (false, reviewResult.Substring(11).Trim(), null);
        }
        else if (reviewResult.StartsWith("PARTIAL:"))
        {
            var parts = reviewResult.Substring(8).Split("Follow-up question:", 2, StringSplitOptions.TrimEntries);
            return (true, parts[0], parts[1]);
        }
        else if (reviewResult.StartsWith("INSUFFICIENT:"))
        {
            return (true, null, reviewResult.Substring(13).Trim());
        }
        else
        {
            LogDebug("Unexpected review result format. Returning original WebAgent response.");
            return (false, webAgentResponse, null);
        }
    }

    private string CombineResponses(List<string> responses)
    {
        return string.Join("\n\n", responses);
    }

    private void LogDebug(string message)
    {
        if (_debug)
        {
            Console.WriteLine($"[DEBUG] BossAgent: {message}");
        }
    }
}
```

## C:\DLLs\GroqAgentLibrary\WebAgent.cs

```
using GroqApiLibrary;
using GroqToolLibrary.WebTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GroqAgentLibrary
{
    public class WebAgent : BaseAgent
    {
        private readonly WebSearchTool _webSearchTool;
        private readonly WebGetLinksTool _webGetLinksTool;
        private readonly WebContentTool _webContentTool;
        private readonly WebWeatherTool _webWeatherTool;
        private readonly bool _debug;

        public WebAgent(ILlmProvider llmProvider, string model, bool debug = false)
            : base(llmProvider, model)
        {
            _webSearchTool = new WebSearchTool();
            _webGetLinksTool = new WebGetLinksTool();
            _webContentTool = new WebContentTool();
            _webWeatherTool = new WebWeatherTool();
            _debug = debug;
        }

        public override async Task<object> ProcessRequestAsync(string request)
        {
            try
            {
                if (IsWeatherQuery(request))
                {
                    return await ProcessWeatherRequestAsync(request);
                }

                // Step 1: Determine the best query for WebSearchTool
                string searchQuery = await DetermineSearchQueryAsync(request);

                // Step 2: Perform web search
                var searchResults = await PerformWebSearchAsync(searchQuery);
                if (!searchResults.Any())
                {
                    return "I'm sorry, but I couldn't find any relevant information for your request.";
                }

                // Step 3: Analyze search results
                var initialSynopsis = await AnalyzeSearchResultsAsync(searchResults, request);
                if (initialSynopsis.Contains("SUFFICIENT"))
                {
                    return initialSynopsis.Replace("SUFFICIENT", "").Trim();
                }

                // Step 4: Retrieve detailed content from top search results
                var detailedContent = await RetrieveDetailedContentAsync(searchResults.ToList());

                // Step 5: Analyze detailed content
                var detailedSynopsis = await AnalyzeDetailedContentAsync(detailedContent, request);
                if (detailedSynopsis.Contains("SUFFICIENT"))
                {
                    return detailedSynopsis.Replace("SUFFICIENT", "").Trim();
                }

                // Step 6: Collect additional links if needed
                var additionalLinks = await CollectAdditionalLinksAsync(searchResults.First().Url);

                // Step 7: Prepare final response
                return await PrepareFinalResponseAsync(detailedSynopsis, additionalLinks, request);
            }
            catch (Exception e)
            {
                LogDebug($"Error in WebAgent: {e.Message}");
                return $"An error occurred while processing your request: {e.Message}";
            }
        }

        private bool IsWeatherQuery(string request)
        {
            string pattern = @"\b(weather|temperature|forecast|rain|snow|wind|climate)\b";
            return Regex.IsMatch(request, pattern, RegexOptions.IgnoreCase);
        }

        private async Task<string> DetermineSearchQueryAsync(string userRequest)
        {
            var prompt = $"Given the user request: '{userRequest}', provide a concise and effective search query to find relevant information. Return only the search query, nothing else.";
            return await Provider.GenerateAsync(prompt);
        }

        private async Task<List<SearchResult>> PerformWebSearchAsync(string query)
        {
            var rawResults = await _webSearchTool.ExecuteAsync(query, 5) as List<Dictionary<string, string>>;
            if (rawResults == null)
            {
                LogDebug("WebSearchTool returned null results");
                return new List<SearchResult>();
            }

            return rawResults.Select(r => new SearchResult
            {
                Title = r.TryGetValue("title", out var title) ? title : string.Empty,
                Url = r.TryGetValue("url", out var url) ? url : string.Empty,
                Description = r.TryGetValue("description", out var description) ? description : string.Empty
            }).ToList();
        }

        private async Task<object> ProcessWeatherRequestAsync(string request)
        {
            try
            {
                string location = await ExtractLocationFromRequestAsync(request);

                if (string.IsNullOrEmpty(location))
                {
                    return "I'm sorry, but I couldn't identify a location in your weather request. Could you please specify a city or location?";
                }

                var weatherData = await _webWeatherTool.ExecuteAsync(location) as Dictionary<string, object>;

                if (weatherData == null)
                {
                    return "I'm sorry, but I couldn't retrieve the weather information. The weather service might be unavailable.";
                }

                if (weatherData.ContainsKey("error"))
                {
                    return $"I'm sorry, but I couldn't retrieve the weather information for {location}. {weatherData["error"]}";
                }

                return FormatWeatherResponse(weatherData, request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessWeatherRequestAsync: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return $"I apologize, but an error occurred while processing your weather request: {ex.Message}";
            }
        }

        private string FormatWeatherResponse(Dictionary<string, object> weatherData, string originalRequest)
        {
            try
            {
                var current = weatherData["current"] as Dictionary<string, object>;
                var forecast = weatherData["forecast"] as Dictionary<string, object>;

                if (current == null || forecast == null)
                {
                    return "I'm sorry, but the weather data seems to be incomplete or in an unexpected format.";
                }

                var response = $"Here's the weather information for {weatherData["location"]}:\n\n";
                response += $"Current conditions: {current["temperature"]}°F, {current["weather"]}\n";
                response += $"Wind: {current["windSpeed"]} {current["windDirection"]}\n\n";

                response += "Today's forecast:\n";
                response += $"Temperature: {forecast["temperature"]}°F\n";
                response += $"Conditions: {forecast["shortForecast"]}\n";
                response += $"Wind: {forecast["windSpeed"]} {forecast["windDirection"]}\n";
                response += $"Chance of precipitation: {forecast["precipitationProbability"]}%\n";

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FormatWeatherResponse: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return "I apologize, but there was an error formatting the weather information.";
            }
        }

        private async Task<string> ExtractLocationFromRequestAsync(string request)
        {
            var prompt = $"Extract the location from this weather-related request: '{request}'. Return only the location name, nothing else.";
            return await Provider.GenerateAsync(prompt);
        }

        private async Task<string> AnalyzeSearchResultsAsync(List<SearchResult> results, string userRequest)
        {
            var resultsText = string.Join("\n", results.Select(r => $"{r.Title}\n{r.Description}\n{r.Url}"));
            var prompt = $"Analyze these search results:\n\n{resultsText}\n\nBased on the user request: '{userRequest}', provide a concise synopsis. If the information is sufficient to answer the request, include 'SUFFICIENT' at the end of your response. If not, don't include 'SUFFICIENT'.";
            return await Provider.GenerateAsync(prompt);
        }

        private async Task<List<string>> RetrieveDetailedContentAsync(List<SearchResult> topResults)
        {
            var detailedContent = new List<string>();
            foreach (var result in topResults)
            {
                var content = await _webContentTool.ExecuteAsync(result.Url) as string;
                if (!string.IsNullOrEmpty(content))
                {
                    detailedContent.Add(content);
                }
            }
            return detailedContent;
        }

        private async Task<string> AnalyzeDetailedContentAsync(List<string> detailedContent, string userRequest)
        {
            var contentText = string.Join("\n\n", detailedContent.Select((content, index) => $"Content {index + 1}:\n{content.Substring(0, Math.Min(1000, content.Length))}"));
            var prompt = $"Analyze this detailed content:\n\n{contentText}\n\nBased on the user request: '{userRequest}', provide a comprehensive synopsis. If the information is sufficient to fully answer the request, include 'SUFFICIENT' at the end of your response. If not, don't include 'SUFFICIENT'.";
            return await Provider.GenerateAsync(prompt);
        }

        private async Task<List<string>> CollectAdditionalLinksAsync(string url)
        {
            var rawLinks = await _webGetLinksTool.ExecuteAsync(url) as List<Dictionary<string, string>>;
            return rawLinks?.Select(l => l["Target"]).Take(5).ToList() ?? new List<string>();
        }

        private async Task<string> PrepareFinalResponseAsync(string synopsis, List<string> additionalLinks, string userRequest)
        {
            var linksText = string.Join("\n", additionalLinks);
            var prompt = $"Given this synopsis:\n\n{synopsis}\n\nAnd these additional links for further information:\n{linksText}\n\nPrepare a final response to the user request: '{userRequest}'. Include the most relevant information from the synopsis and, if appropriate, mention that further information can be found at the provided links.";
            return await Provider.GenerateAsync(prompt);
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

            string model = "mixtral-8x7b-32768";  // You might want to make this configurable

            ILlmProvider llmProvider = new GroqLlmProvider(apiKey, model);

            var bossAgent = new BossAgent(llmProvider, model, debug: true);

            while (true)
            {
                Console.Write("\nEnter your request (or 'exit' to quit): ");
                string userRequest = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userRequest) || userRequest.ToLower() == "exit")
                {
                    Console.WriteLine("Thank you for using GroqAgents CLI. Goodbye!");
                    break;
                }

                object response = await bossAgent.ProcessRequestAsync(userRequest);
                Console.WriteLine("\nResponse:");
                Console.WriteLine(response.ToString());

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }
}
```

