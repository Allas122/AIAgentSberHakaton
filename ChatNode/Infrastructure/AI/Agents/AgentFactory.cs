using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Repositories;
using GigaChat.Net;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.AI.Agents;

public class AgentFactory(IServiceProvider sp)
{
    public ConsultingAgent CreateConsultingAgent(Func<string, Task> statusHandler, AgentSession session)
    {
        return ActivatorUtilities.CreateInstance<ConsultingAgent>(sp, statusHandler, session);
    }

    public ManualParserAgent CreateManualParserAgent(Guid manualId)
    {
        return ActivatorUtilities.CreateInstance<ManualParserAgent>(sp, manualId);
    }

    public ApplicationReviewAgent CreateApplicationReviewAgent(Func<string, Task> statusHandler, AgentSession session)
    {
        return ActivatorUtilities.CreateInstance<ApplicationReviewAgent>(sp, statusHandler, session);
    }

    public LetterAgent CreateLetterAgent(LetterSession session)
    {
        return ActivatorUtilities.CreateInstance<LetterAgent>(sp, session);
    }
    
}