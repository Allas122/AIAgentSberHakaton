using ChatNode.Infrastructure.Configuration.Options;
using Domain.Repositories;
using GigaChat.Net;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.AI.Agents;

public class AgentFactory(IServiceProvider sp)
{
    public ConsultingAgent CreateConsultingAgent(Action<string> statusHandler, Guid manualId)
    {
        var repo = sp.GetRequiredService<IManualRepository>();
        var giga = sp.GetRequiredService<IGigaChatClient>();
        var options = sp.GetRequiredService<IOptions<GigaChatOptions>>();
        
        return new ConsultingAgent(repo, giga, options, statusHandler, manualId);
    }
}