


using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;

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

#pragma warning disable OPENAI001
AssistantClient assistantClient = openAIClient.GetAssistantClient();
OpenAIFileClient fileClient = openAIClient.GetOpenAIFileClient();

using Stream document = BinaryData.FromBytes("""
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
    """u8.ToArray()).ToStream();

//创建内存中示例文档并将其上传到 OpenAIFileClient
OpenAIFile salesFile = fileClient.UploadFile(
    document,
    "monthly_sales.json",
    FileUploadPurpose.Assistants);

//通过 AssistantCreationOptions启用文件搜索和代码解释器工具功能
AssistantCreationOptions assistantOptions = new()
{
    Name = "Example: Contoso sales RAG",
    Instructions =
        "You are an assistant that looks up sales data and helps visualize the information based"
        + " on user queries. When asked to generate a graph, chart, or other visualization, use"
        + " the code interpreter tool to do so.",
    Tools =
    {
        new FileSearchToolDefinition(), // Enable the assistant to search and access files
        new CodeInterpreterToolDefinition(), // Enable the assistant to run code for data analysis
    },
    ToolResources = new()
    {
        FileSearch = new()
        {
            NewVectorStores =
            {
                new VectorStoreCreationHelper([salesFile.Id]),
            }
        }
    },
};

//创建 Assistant 和线程来管理用户与助手之间的交互：
// Create the assistant
Assistant assistant = assistantClient.CreateAssistant(model, assistantOptions);

// Configure and create the conversation thread
ThreadCreationOptions threadOptions = new()
{
    InitialMessages = { "How well did product 113045 sell in February? Graph its trend over time." }
};

ThreadRun threadRun = assistantClient.CreateThreadAndRun(assistant.Id, threadOptions);

// Sent the prompt and monitor progress until the thread run is complete
do
{
    Thread.Sleep(TimeSpan.FromSeconds(1));
    threadRun = assistantClient.GetRun(threadRun.ThreadId, threadRun.Id);
}
while (!threadRun.Status.IsTerminal);

// Get the messages from the thread run
var messages = assistantClient.GetMessagesAsync(
    threadRun.ThreadId,
    new MessageCollectionOptions()
    {
        Order = MessageCollectionOrder.Ascending
    });

//打印消息，并从与助手的对话中保存生成的图像：
await foreach (ThreadMessage message in messages)
{
    // Print out the messages from the assistant
    Console.Write($"[{message.Role.ToString().ToUpper()}]: ");
    foreach (MessageContent contentItem in message.Content)
    {
        if (!string.IsNullOrEmpty(contentItem.Text))
        {
            Console.WriteLine($"{contentItem.Text}");

            if (contentItem.TextAnnotations.Count > 0)
            {
                Console.WriteLine();
            }

            // Include annotations, if any
            foreach (TextAnnotation annotation in contentItem.TextAnnotations)
            {
                if (!string.IsNullOrEmpty(annotation.InputFileId))
                {
                    Console.WriteLine($"* File citation, file ID: {annotation.InputFileId}");
                }
                if (!string.IsNullOrEmpty(annotation.OutputFileId))
                {
                    Console.WriteLine($"* File output, new file ID: {annotation.OutputFileId}");
                }
            }
        }
        // Save the generated image file
        if (!string.IsNullOrEmpty(contentItem.ImageFileId))
        {
            OpenAIFile imageInfo = fileClient.GetFile(contentItem.ImageFileId);
            BinaryData imageBytes = fileClient.DownloadFile(contentItem.ImageFileId);
            using FileStream stream = File.OpenWrite($"{imageInfo.Filename}.png");
            imageBytes.ToStream().CopyTo(stream);

            Console.WriteLine($"<image: {imageInfo.Filename}.png>");
        }
    }
    Console.WriteLine();
}