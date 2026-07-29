using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions.Abstractions;

public interface IFunctionToolsSet
{
    public static IReadOnlyList<IChatFunctionTool> FunctionTools { get; }
}