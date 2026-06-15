
using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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

IChatClient client = new ChatClientBuilder(chatClient).UseFunctionInvocation().Build();

var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create((string location, string unit)=>{        
        Console.WriteLine($"调用了获取天气的函数get_current_weather，参数：location={location}, unit={unit}");
        // 调用其他应用API
        return "雨或细雨期间，15摄氏度";
    },
    "get_current_weather",
    "获取给定位置的当前天气")]
};
List<ChatMessage> chatHistory = [new (ChatRole.System, "你是一个天气助手，你可以根据位置获取天气天气。")];
chatHistory.Add(new ChatMessage(ChatRole.User, "现在杭州的天气怎么样？"));
ChatResponse reponse = await client.GetResponseAsync(chatHistory, chatOptions);

Console.WriteLine($"模型响应:{reponse}");
Console.WriteLine($"模型响应Text:{reponse.Text}");



