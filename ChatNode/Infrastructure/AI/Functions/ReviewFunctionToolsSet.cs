using System.Text.Json;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.AI.Review;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Database.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class ReviewFunctionToolsSet(
    IReviewRepository repository,
    IAnonymizeClient anonymizeClient,
    Guid chatId,
    Guid ownerId,
    string sessionId) : IFunctionToolsSet
{
    private readonly RepeatCallGuard _guard = new();

    private const int MaxFindingsPerCriterion = 5;

    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<ReviewLookupArguments>(
            name: "get_application_review",
            description: "Получить результат последней проверки заявки: баллы по критериям, " +
                         "вывод по каждому и найденные замечания. " +
                         "Вызывай, когда пользователь спрашивает про оценку, баллы или ошибки в заявке.",
            handler: GetReview,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["status"] = FunctionParameter.String(
                        "Фильтр по выводу: Violated — нарушения, Questionable — требует уточнения, " +
                        "NotFound — раздел не найден, NoIssues — без замечаний. " +
                        "Не указывать — вернутся все критерии.",
                        ["Violated", "Questionable", "NotFound", "NoIssues"])
                }
            )
        )
    ];

    public async Task<string> GetReview(ReviewLookupArguments arguments)
    {
        if (_guard.IsRepeat("get_application_review", arguments.Status))
        {
            return RepeatCallGuard.RepeatResponse("get_application_review");
        }

        var review = await repository.GetLatestAsync(chatId)
                     ?? await repository.GetLatestForOwnerAsync(ownerId);

        if (review is null)
        {
            return ToolJson.Serialize(new ReviewActionReturn(
                "Empty",
                "Проверенных заявок не найдено. Предложи пользователю загрузить заявку на проверку " +
                "и не выдумывай замечания."));
        }

        var criteria = review.Criteria.OrderBy(c => c.Index).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(arguments.Status))
        {
            if (!Enum.TryParse<CriterionStatus>(arguments.Status, ignoreCase: true, out var status))
            {
                return ToolJson.Serialize(new ReviewActionReturn(
                    "Error",
                    $"Неизвестный фильтр \"{arguments.Status}\". " +
                    "Допустимо: Violated, Questionable, NotFound, NoIssues."));
            }

            criteria = criteria.Where(c => c.Status == status);
        }

        var views = new List<ReviewCriterionView>();

        foreach (var criterion in criteria)
        {
            views.Add(new ReviewCriterionView(
                criterion.Index,
                await HideAsync(criterion.Name),
                ReviewRenderer.StatusText(criterion.Status),
                criterion.Score,
                criterion.MaxScore,
                await HideAsync(criterion.Explanation),
                await HideManyAsync(ParseFindings(criterion))));
        }

        return ToolJson.Serialize(new GetReviewReturn(
            "Ok",
            review.FileName,
            review.TotalScore,
            review.MaxScore,
            review.UnverifiedCount,
            views));
    }

    private static IReadOnlyList<string> ParseFindings(StoredReviewCriterion criterion)
    {
        if (string.IsNullOrWhiteSpace(criterion.Findings)) return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<FindingRow>>(criterion.Findings) ?? [];

            return parsed
                .Take(MaxFindingsPerCriterion)
                .Select(row => $"[{row.Type}] {row.Content}")
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string> HideAsync(string text) =>
        string.IsNullOrWhiteSpace(text) ? text : await anonymizeClient.AnonymizeAsync(text, sessionId);

    private async Task<IReadOnlyList<string>> HideManyAsync(IReadOnlyList<string> texts) =>
        texts.Count == 0 ? [] : await anonymizeClient.AnonymizeBatchAsync(texts, sessionId);

    private sealed record FindingRow(string Content, string Type, string Source);
}
