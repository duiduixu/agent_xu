// Console.WriteLine("Hello, World!");


using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
String modeltest = config["DeepSeekModelName"];
string model = GetRequiredConfigValue(config, "DeepSeekModelName");
string apiKey = GetRequiredConfigValue(config, "DeepSeekAIKey");

Console.WriteLine($"Model: {model}, API key configured: {!string.IsNullOrWhiteSpace(apiKey)}");

var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri("https://api.deepseek.com")
};

var aiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
var chatClient = aiClient.GetChatClient(model).AsIChatClient();

List<ChatMessage> chatHistory =
[
    new ChatMessage(ChatRole.System, """
            您是一位友好的徒步旅行爱好者，帮助人们发现他们所在地区有趣的徒步旅行。
            第一次打招呼时你会自我介绍。
            在帮助别人时，你总是向他们询问这些信息
            告知您提供的徒步建议：

            1. 他们想要徒步旅行的地点
            2. 他们想要什么强度的徒步旅行
            然后，您将为附近长度各异的徒步旅行提供三个建议
            当你得到这些信息后。您还将分享一个有趣的事实
            提出建议时，要考虑徒步旅行的当地性质。在你的最后
            回复，询问是否还有其他需要帮助的地方。
    """)
];

while(true)
{
    Console.WriteLine("请输入您的问题：");
    String userPrompt = Console.ReadLine() ?? "";
    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));
    Console.WriteLine("AI回复：");
    String response = "";
    await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        response += item.Text;
    }
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}

static string GetRequiredConfigValue(IConfiguration configuration, string key)
{
    return !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new InvalidOperationException($"Missing required configuration value: {key}");
}
