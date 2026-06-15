


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

var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
var chatClient = openAIClient.GetChatClient(model).AsIChatClient();

string salesJson = """
    {
        "description": "This document contains the sale history data for Contoso products.",
        "sales": [
            {
                "month": "January",
                "by_product": {
                    "113043": 15,
                    "113045": 12,
                    "113049": 2
                }
            },
            {
                "month": "February",
                "by_product": {
                    "113045": 22
                }
            },
            {
                "month": "March",
                "by_product": {
                    "113045": 16,
                    "113055": 5
                }
            }
        ]
    }
    """;

string prompt = $$"""
你是一个销售数据分析助手，只能根据下面提供的 JSON 数据回答，不要编造数据。

请完成两件事：
1. 回答产品 113045 在 February 的销量是多少。
2. 用纯文本总结它在 January、February、March 的趋势。

如果用户要求图表，也只返回文本结论，因为当前后端不支持文件检索和代码解释器。

数据如下：
{{salesJson}}
""";

var response = await chatClient.GetResponseAsync(
    prompt,
    new ChatOptions
    {
        MaxOutputTokens = 400,
    });

Console.WriteLine(response);