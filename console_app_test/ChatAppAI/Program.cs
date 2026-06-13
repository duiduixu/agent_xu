

//Console.WriteLine("Hello, World!");

using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string model = config["DeepSeekModelName"];
string apiKey = config["DeepSeekAIKey"];

Console.WriteLine($"Model: {model}, APIKey: {apiKey}");

var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri("https://api.deepseek.com")
};

var aiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

var chatClient = aiClient.GetChatClient(model).AsIChatClient();

// var response = await chatClient.GetResponseAsync("您好，请写一篇关于AI的作文，要求字数在200字左右。",
//     new ChatOptions{MaxOutputTokens = 100});

String text = File.ReadAllText("test/text_test1.md");
String prompt = $"""
Summarize the the following text in 100 words or less:
{text}
""";
var response = await chatClient.GetResponseAsync(prompt,
    new ChatOptions{MaxOutputTokens = 100});

Console.WriteLine(response);


