using System.Text.Json;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Mappers;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class ManualFunctionsToolsSet(IManualRepository repository, Guid manualId) : IFunctionToolsSet
{
    public IReadOnlyList<IChatFunctionTool> FunctionTools => [
        FunctionTool.Create<AddManualPartArguments>(
            name: "add_manual_part",
            description: "Добавляет часть мануала в хранилище, никак не влияет на сам мануал.",
            handler: AddManualPartHandler,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["title"] = FunctionParameter.String("Титул части мануала."),
                    ["content"] = FunctionParameter.String(
                        @"Прямая цитата из мануала, содержащая правила и основной смысл этой части мануала, исключая заголовки."),
                    ["navigation"] = FunctionParameter.String(
                        @"Это строка, которая описывает под какими заголовками находится эта часть мануала. 
                                 Должна включать основные заголовки этой части мануала через разделитель >."
                        )
                },
                required: ["title", "content", "navigation"]
                )
        ),
        FunctionTool.Create<NullableArguments>(
            name: "get_manual_full_navigation",
            description: @"Возвращает текущий список всех добавленных или изменённых разделов мануала через запятые.",
            handler: GetManualFullNavigation
        ),
        FunctionTool.Create<AddManualFullNavigationPartArgument>(
            name: "add_manual_navigation_header",
            description: @"Добавляет в текущее состояние навигации по мануалу новую навигационную строку.",
            handler: AddManualFullNavigation,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["navigation"] = FunctionParameter.String(
                        @"Заголовок раздела мануала, который ты хочешь добавить."
                    )
                },
                required: ["navigation"]
            )
        ),
        FunctionTool.Create<SearchArguments>(
            name: "search_in_manual",
            description: "Позволяет по query найти в RAG часть мануала.",
            handler: SearchInManualHandler,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["query"] = FunctionParameter.String(
                            "Строка, по которой нужно что-то найти в мануале."
                    ),
                    ["limit"] = FunctionParameter.Integer(
                        "Какое максимальное количество совпадений тебе нужно"
                        )
                },
                required: ["query", "limit"]
            ))
    ];


    public async Task<string> SearchInManualHandler(SearchArguments arguments)
    {
        var parts = await repository.KnnSearchManualPartAsync(arguments.Query, arguments.Limit);
        parts.Select(
            part => part.Content);
        Console.WriteLine($"[SEARCHING]:{arguments.Query}:{arguments.Limit})");
        return JsonSerializer.Serialize(parts);
    }
    
    public async Task<string> AddManualPartHandler(AddManualPartArguments arguments)
    {
        var manualPartDto = new ManualPartDto
        (
            Guid.NewGuid(),
            manualId, 
            arguments.Title,
            arguments.Content,
            arguments.Navigation
        );

        if (string.IsNullOrWhiteSpace(arguments.Content) || arguments.Content.Length < 10)
        {
            return "{\"status\": \"error\", \"message\": \"Content too short\"}";
        }

        Console.WriteLine($"[INDEXING]: {arguments.Navigation} -> {arguments.Title} ({arguments.Content.Length} chars)");

        var manualPartId = await repository.CreateManualPartAsync(manualPartDto.MapToManualPart());
        
        return $"{{\"manualPartId\":\"{manualPartId}\"}}";
    }

    public async Task<string> GetManualFullNavigation(NullableArguments s)
    {
        var manualNavigation = (await repository.GetManualAsync(manualId)).Navigation;
        var res = new GetManualFullNavigationReturn(manualNavigation);
        return JsonSerializer.Serialize(res);
    }
    
    public async Task<string> AddManualFullNavigation(AddManualFullNavigationPartArgument arguments)
    {
        var manual = (await repository.GetManualAsync(manualId)).MapToManualDto();
        var newManual = new ManualDto(manual.Id, manual.Title, manual.Navigation+", "+arguments.Navigation);
        await repository.UpdateManualAsync(newManual.MapToManual());
        var res = new SetManualFullNavigationReturn("Ok", "Навигация обновлена");
        return JsonSerializer.Serialize(res);
    }
    
}