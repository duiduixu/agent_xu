

using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;

static string GetRequiredConfigValue(IConfiguration configuration, string key)
{
    return !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new InvalidOperationException($"Missing required configuration value: {key}");
}


IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();


string model = GetRequiredConfigValue(config, "DeepSeekModelName");
string apiKey = GetRequiredConfigValue(config, "DeepSeekAIKey");

Console.WriteLine($"Model: {model}, API key configured: {!string.IsNullOrWhiteSpace(apiKey)}");

var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri("https://api.deepseek.com")
};

var aiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
var chatClient = aiClient.GetChatClient(model).AsIChatClient();


#region 方法一：直接让模型输出 JSON 字符串
// string jsonSchema = JsonSerializer.Serialize(new SearchResult());
// string systemPrompt = $@"
// 你是一个数据提取助手。请严格按照以下 JSON Schema 输出内容，不要包含任何 markdown 格式（不要使用 ```json）：
// {jsonSchema}
// 示例：
// 输入：查询关于2026年Maui旅游的信息
// 输出：{{""Summary"":""2026年Maui旅游概览"",""Confidence"":0.95}}
// ";
// Console.WriteLine($"系统提示：{systemPrompt}");
// string userPrompt = "查询关于2026年杭州旅游的信息";
// List<ChatMessage> chatHistory = new()
// {
//     new ChatMessage(ChatRole.System, systemPrompt),
//     new ChatMessage(ChatRole.User, userPrompt)
// };
// String response = "";
// Console.WriteLine($"开始响应：");
// await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory))
// {
//     Console.Write(item.Text);
//     response += item.Text;
// }
// Console.WriteLine($"");
// Console.WriteLine($"模型回复：{response}");
#endregion

#region 方法二：使用 Semantic Kernel 自动解析
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: model,
    apiKey: apiKey,
    endpoint: new Uri("https://api.deepseek.com")
);
var kernel = builder.Build();
var chatService = kernel.GetRequiredService<IChatCompletionService>();
String userPrompt = "提取这段文本里的个人信息：张三1990年1月1日出生，今年25岁 。";
ChatHistory chatHistory = new();
chatHistory.AddSystemMessage("你必须输出纯粹的 JSON，格式如下：{\"Name\":\"...\",\"Age\":0}");
chatHistory.AddUserMessage(userPrompt);
Console.WriteLine($"用户提示：{userPrompt}");
var result = await chatService.GetChatMessageContentAsync(chatHistory);
Console.WriteLine($"模型回复：{result}");
#endregion

public class SearchResult
{
    public string? Summary { get; set; }
    public double Confidence { get; set; }
}
