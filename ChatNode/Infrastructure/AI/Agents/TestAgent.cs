using ChatNode.Infrastructure.AI.Functions;
using Domain.Entities;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public record ArgumentConsole(string Message);

public class TestAgent(
    IGigaChatClient gigaChatClient,
    IManualRepository manualRepository
)
{
    public async Task InvokeAsync(string fullPrompt,CancellationToken ct)
    {
        var manualId = await manualRepository.CreateManualAsync(new Manual(Guid.NewGuid(), "Инструкция по грантам", ""));
    
        int chunkSize = 4000;
        int overlapSize = 500; 
        int offset = 0;
        string lastOverlap = "";

        while (offset < fullPrompt.Length)
        {
            int length = Math.Min(chunkSize, fullPrompt.Length - offset);
            string currentNewText = fullPrompt.Substring(offset, length);
        
            string promptWithContext = $"[ПРОШЛЫЙ КОНТЕКСТ ДЛЯ СПРАВКИ]:\n...{lastOverlap}\n\n" +
                                       $"[НОВЫЙ ТЕКСТ ДЛЯ ОБРАБОТКИ]:\n{currentNewText}";

            await RunAsync(promptWithContext, manualId, ct);

            lastOverlap = currentNewText.Substring(Math.Max(0, currentNewText.Length - overlapSize));
            offset += length; 
        }
    }

    private async Task RunAsync(string prompt, Guid manualId,CancellationToken ct)
    {
        ManualFunctionsToolsSet manualFunctionsToolsSet = new ManualFunctionsToolsSet(manualRepository, manualId);
        
        Chat chat = new Chat()
        {
            Messages = [
                Messages.System(@"
Ты — агент-сегментатор. Твоя работа — наполнять базу знаний(RAG). 
Поскольку ты получаешь текст частями, используй функции навигации как свою ""длительную память"" уже имеющихся заголовков.

Алгоритм твоих действий:
1. Найти логическую часть мануала, добавить её через AddManualPart.
2. Добавить её заголовки через add_manual_navigation_header.
Требования к части мануала - Должно быть логически отделено и включать в себя вырезанный текст из мануала.
Игнорировать какие-либо логические части мануала строго запрещено !
Ограничение: Все части мануала в общем(title+content+navigation) должны умещаться в 400 токенов Если не хватает, дроби 
на 2 части мануала 1 логическую часть, ток так чтобы без прерываний и целостность предложений осталась. Чем меньше текста
ты засунешь, тем лучше.
Требование к навигации в мануале: Должна содержать список всех Navigation свойств из Navigation частей мануала.
"),
                Messages.User(prompt)
            ],
            FunctionCall = FunctionCallMode.Auto,
            Model = "GigaChat-2-Max"
        };

        await gigaChatClient.ChatWithToolsAsync(chat, manualFunctionsToolsSet.FunctionTools, maxToolCalls: 40,cancellationToken:ct);
    }
}