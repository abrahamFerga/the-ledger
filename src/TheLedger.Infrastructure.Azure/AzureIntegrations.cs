using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TheLedger.Infrastructure.Azure;

public static class AzureIntegrations
{
    /// <summary>
    /// Registers a concrete Azure OpenAI <see cref="IChatClient"/> when
    /// <c>Ai:AzureOpenAI:Endpoint</c> + <c>Ai:AzureOpenAI:Deployment</c> are configured (Managed
    /// Identity auth), which activates the LLM-forward categorizer (ADR-0004). When unconfigured,
    /// nothing is registered and the <c>CompositeCategorizer</c> stays rules-only.
    /// </summary>
    public static IServiceCollection AddAzureAiCategorization(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["Ai:AzureOpenAI:Endpoint"];
        var deployment = configuration["Ai:AzureOpenAI:Deployment"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return services;
        }

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        services.AddSingleton<IChatClient>(azureClient.GetChatClient(deployment).AsIChatClient());
        return services;
    }
}
