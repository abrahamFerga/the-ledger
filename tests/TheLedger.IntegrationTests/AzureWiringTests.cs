using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Infrastructure.Azure;
using Xunit;

namespace TheLedger.IntegrationTests;

public class AzureWiringTests
{
    [Fact]
    public void Azure_openai_is_not_registered_without_config()
    {
        var services = new ServiceCollection();
        services.AddAzureAiCategorization(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IChatClient>());
    }

    [Fact]
    public void Azure_openai_is_registered_when_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:AzureOpenAI:Endpoint"] = "https://example.openai.azure.com/",
                ["Ai:AzureOpenAI:Deployment"] = "gpt-4o",
            })
            .Build();
        services.AddAzureAiCategorization(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IChatClient>());
    }
}
