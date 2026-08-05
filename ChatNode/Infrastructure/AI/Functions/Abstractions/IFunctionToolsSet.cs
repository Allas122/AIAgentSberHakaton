using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions.Abstractions;

public interface IFunctionToolsSet
{
    public IReadOnlyList<IChatFunctionTool> FunctionTools { get; }
}