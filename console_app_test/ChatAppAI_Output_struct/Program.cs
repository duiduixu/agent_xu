
using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string model = GetRequiredConfigValue(config, "OpenAIModel");
string apiKey = GetRequiredConfigValue(config, "OpenAIApiKey");

Console.WriteLine($"Model: {model}, API key configured: {!string.IsNullOrWhiteSpace(apiKey)}");

var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri("https://api.openai.com/v1")
};

var aiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

var chatClient = aiClient.GetChatClient(model).AsIChatClient();


//DeepSeek-V4-Pro 模型不支持完整的、带有 Schema 验证的“结构化输出”。虽然 DeepSeek-V4-Pro 模型本身支持 JSON 输出，但其完整能力 (如 json_schema) 需要特定服务商的支持
string review = "I'm happy with the product!";
var response = await chatClient.GetResponseAsync<Sentiment>($"What's the sentiment of this review? {review}");
Console.WriteLine($"Sentiment: {response.Result}");




static string GetRequiredConfigValue(IConfiguration configuration, string key)
{
    return !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new InvalidOperationException($"Missing required configuration value: {key}");
}

public enum Sentiment
{
    Positive,
    Negative,
    Neutral
}