using Domain.Entities;

namespace ChatNode.Infrastructure.AI.Functions;

public enum PinAction
{
    Created,
    Updated,
    Deleted
}

public record PinJournalEntry(PinAction Action, PinType Type, string Content);
