using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI;
using VectorDataAI;

static string GetRequiredConfigValue(IConfiguration configuration, string key)
{
    return !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new InvalidOperationException($"Missing required configuration value: {key}");
}


List<CloudService> cloudServices =
[
new() {
            Key = 0,
            Name = "Azure App Service",
            Description = "Host .NET, Java, Node.js, and Python web applications and APIs in a fully managed Azure service. You only need to deploy your code to Azure. Azure takes care of all the infrastructure management like high availability, load balancing, and autoscaling."
    },
    new() {
            Key = 1,
            Name = "Azure Service Bus",
            Description = "A fully managed enterprise message broker supporting both point to point and publish-subscribe integrations. It's ideal for building decoupled applications, queue-based load leveling, or facilitating communication between microservices."
    },
    new() {
            Key = 2,
            Name = "Azure Blob Storage",
            Description = "Azure Blob Storage allows your applications to store and retrieve files in the cloud. Azure Storage is highly scalable to store massive amounts of data and data is stored redundantly to ensure high availability."
    },
    new() {
            Key = 3,
            Name = "Microsoft Entra ID",
            Description = "Manage user identities and control access to your apps, data, and resources."
    },
    new() {
            Key = 4,
            Name = "Azure Key Vault",
            Description = "Store and access application secrets like connection strings and API keys in an encrypted vault with restricted access to make sure your secrets and your application aren't compromised."
    },
    new() {
            Key = 5,
            Name = "Azure AI Search",
            Description = "Information retrieval at scale for traditional and conversational search applications, with security and options for AI enrichment and vectorization."
    }
];

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
String modeltest = config["DeepSeekModelName"];
string model = "text-embedding-3-small";//GetRequiredConfigValue(config, "OpenAIModel");
string apiKey = GetRequiredConfigValue(config, "OpenAIApiKey");

Console.WriteLine($"Model: {model}, API key configured: {!string.IsNullOrWhiteSpace(apiKey)}");

IEmbeddingGenerator<String, Embedding<float>> embeddingGenerator = new OpenAIClient(new ApiKeyCredential(apiKey)).GetEmbeddingClient(model: model).AsIEmbeddingGenerator(); 

//创建并填充向量存储
var vectorStore = new InMemoryVectorStore();
VectorStoreCollection<int, CloudService> cloudServicesStore = vectorStore.GetCollection<int, CloudService>("cloudservices");
await cloudServicesStore.EnsureCollectionExistsAsync();
foreach(CloudService cloudService in  cloudServices)
{
    cloudService.Vector = await embeddingGenerator.GenerateVectorAsync(cloudService.Description);
    await cloudServicesStore.UpsertAsync(cloudService);
}

//使用向量存储执行搜索
string query = "Which Azure service should I use to store my Word documents?";
ReadOnlyMemory<float> queryEmbbeding = await embeddingGenerator.GenerateVectorAsync(query);
IAsyncEnumerable<VectorSearchResult<CloudService>> results = cloudServicesStore.SearchAsync(queryEmbbeding, top: 1);
await foreach (VectorSearchResult<CloudService> result in results)
{
    Console.WriteLine($"raw response: {result.Record}");

    Console.WriteLine($"Name: {result.Record.Name}");
    Console.WriteLine($"Description: {result.Record.Description}");
    Console.WriteLine($"Vector match score: {result.Score}");
}
