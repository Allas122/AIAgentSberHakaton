using System.Text;
using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public class ApplicationReviewAgent : IAgent
{
    private sealed record SectionsOutcome(
        List<(ApplicationSectionDto Section, int Number)> Failed,
        int Succeeded,
        bool ServiceDown);

    private static readonly string[] SectionFunctions =
    [
        "search_in_manual",
        "create_pin",
        "update_pin",
        "delete_pin",
        "search_pins",
        "get_pins"
    ];

    private static readonly PinType[] VerdictPinOrder = [PinType.Mistake, PinType.Attention, PinType.WhatToCheck];

    private const int MinToolCallsPerSection = 3;
    private const int MinSectionChars = 400;
    private const int MaxPinContentChars = 300;
    private const int FallbackDigestChars = 8000;
    private const int MinOutlineLineChars = 18;
    private const int DuplicatePrefixChars = 80;
    private const int OutageThreshold = 3;
    private const int EllipsisReserve = 4;
    private const int MaxReportedFailures = 5;
    private const int CriteriaSearchLimit = 5;
    private const int MaxCriteriaNamesChars = 400;
    private const int MinMemoChars = 150;
    private const int MemoLineChars = 220;
    private const int MemoBudget = 3000;
    private const int MemoHeadChars = 400;
    private const string CriteriaQuery = "критерии оценки заявки баллы";
    private const string ToolCallLimitMarker = "exceeded the maximum of";

    private readonly IGigaChatClient _gigaChatClient;
    private readonly IManualRepository _manualRepository;
    private readonly IPinRepository _pinRepository;
    private readonly GigaChatOptions _gigaChatOptions;
    private readonly Action<string> _statusHandler;
    private readonly Guid _chatId;
    private readonly Guid _manualId;

    private string _memo = string.Empty;

    public ApplicationReviewAgent(
        IGigaChatClient gigaChatClient,
        IManualRepository manualRepository,
        IPinRepository pinRepository,
        IOptions<GigaChatOptions> gigaChatOptions,
        Action<string> statusHandler,
        AgentSession session)
    {
        _gigaChatClient = gigaChatClient;
        _manualRepository = manualRepository;
        _pinRepository = pinRepository;
        _gigaChatOptions = gigaChatOptions.Value;
        _statusHandler = statusHandler;
        _chatId = session.ChatId;
        _manualId = session.ManualId;
    }

    public async Task<string> ReviewAsync(
        IReadOnlyList<ApplicationSectionDto> sections,
        string? userComment,
        CancellationToken ct)
    {
        if (sections.Count == 0)
        {
            return "Не удалось извлечь текст из документа — файл пустой или состоит только из картинок.";
        }

        var pieces = PackSections(sections);
        var failedSections = new List<(ApplicationSectionDto Section, int Number)>();

        Console.WriteLine(
            $"[REVIEW STARTED]: model={_gigaChatOptions.ApplicationReviewAgentModel}, " +
            $"разделов={sections.Count}, запросов={pieces.Count}, бюджет={Budget} симв., " +
            $"вызовов на фрагмент={SectionToolCalls}");

        try
        {
            _statusHandler("Проверяю доступность сервиса ИИ...");

            if (!await IsServiceReachableAsync(ct)) return ServiceUnavailableMessage();

            _statusHandler("Поднимаю критерии оценки из методички...");
            var criteria = await ExtractCriteriaAsync(ct);
            var criteriaNames = ToCriteriaNames(criteria);

            var outcome = await ReviewSectionsAsync(pieces, userComment, criteriaNames, ct);

            if (outcome.ServiceDown) return ServiceUnavailableMessage();

            failedSections = outcome.Failed;

            if (failedSections.Count > 0)
            {
                failedSections = await RetryFailedSectionsAsync(
                    failedSections, pieces.Count, userComment, criteriaNames, ct);
            }

            var reviewed = pieces.Count - failedSections.Count;

            _statusHandler("Сверяю состав заявки с критериями...");
            var coverage = await CheckCoverageAsync(sections, criteria, ct);

            _statusHandler("Собираю итоговый разбор заявки...");

            var verdict = await BuildVerdictAsync(criteria, coverage, userComment, reviewed, ct);

            return failedSections.Count == 0
                ? verdict
                : verdict + FailureNote(failedSections.Select(failed => failed.Section.Title).ToList(), pieces.Count);
        }
        catch (OperationCanceledException)
        {
            return "Проверка заявки прервана. Заметки, сделанные до остановки, сохранены в этом чате.";
        }
    }
    
    private async Task<bool> IsServiceReachableAsync(CancellationToken ct)
    {
        try
        {
            await _gigaChatClient.GetTokenAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[REVIEW PREFLIGHT]: авторизация в GigaChat не прошла -> {GigaChatRetry.Describe(ex)}");
            return false;
        }
    }

    private async Task<SectionsOutcome> ReviewSectionsAsync(
        IReadOnlyList<ApplicationSectionDto> pieces,
        string? userComment,
        string criteriaNames,
        CancellationToken ct)
    {
        var failed = new List<(ApplicationSectionDto Section, int Number)>();

        var succeeded = 0;
        var transientFailures = 0;

        for (var index = 0; index < pieces.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var section = pieces[index];
            _statusHandler($"Проверяю раздел {index + 1}/{pieces.Count}: {Shorten(section.Title)}");

            try
            {
                await ReviewSectionAsync(section, index + 1, pieces.Count, userComment, criteriaNames, ct);
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REVIEW FAILED]: {section.Title} -> {GigaChatRetry.Describe(ex)}");
                failed.Add((section, index + 1));

                if (!GigaChatRetry.IsTransient(ex) || succeeded > 0) continue;

                if (++transientFailures >= OutageThreshold)
                {
                    Console.WriteLine(
                        $"[REVIEW ABORTED]: {OutageThreshold} обращения подряд не прошли и ни одно не удалось — " +
                        "останавливаю проверку, сервис недоступен");

                    _statusHandler("Сервис ИИ не отвечает — останавливаю проверку.");

                    return new SectionsOutcome(failed, succeeded, true);
                }
            }
        }

        return new SectionsOutcome(failed, succeeded, false);
    }

    private async Task<List<(ApplicationSectionDto Section, int Number)>> RetryFailedSectionsAsync(
        IReadOnlyList<(ApplicationSectionDto Section, int Number)> failed,
        int total,
        string? userComment,
        string criteriaNames,
        CancellationToken ct)
    {
        var stillFailed = new List<(ApplicationSectionDto Section, int Number)>();

        _statusHandler($"Повторяю проверку {failed.Count} фрагмент(ов)...");
        Console.WriteLine($"[REVIEW RETRY]: повторяю {failed.Count} фрагмент(ов) через {_gigaChatOptions.OperationRetryDelaySeconds:0.#} с");

        await Task.Delay(TimeSpan.FromSeconds(_gigaChatOptions.OperationRetryDelaySeconds), ct);

        foreach (var (section, number) in failed)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ReviewSectionAsync(section, number, total, userComment, criteriaNames, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REVIEW RETRY FAILED]: {section.Title} -> {GigaChatRetry.Describe(ex)}");
                stillFailed.Add((section, number));
            }
        }

        return stillFailed;
    }

    public Task<string> InvokeAsync(string prompt, CancellationToken ct) =>
        ReviewAsync([new ApplicationSectionDto("Документ", prompt)], userComment: null, ct);

    private static string ToCriteriaNames(string criteria)
    {
        if (criteria.Length == 0) return string.Empty;

        var names = criteria
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("ШКАЛА", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split('—', '-')[0].Trim().TrimStart('•', '*', '.', ' '))
            .Where(name => name.Length > 0);

        return Truncate(string.Join("; ", names), MaxCriteriaNamesChars);
    }

    private int Budget => Math.Max(_gigaChatOptions.ApplicationSectionChars, MinSectionChars);

    private int SectionToolCalls =>
        Math.Max(_gigaChatOptions.ApplicationSectionToolCalls, MinToolCallsPerSection);


    private (IReadOnlyList<IChatFunctionTool> Tools, PinFunctionToolsSet Pins) BuildSectionTools()
    {
        var manualTools = new ManualFunctionsToolsSet(_manualRepository, _manualId).FunctionTools;

        var pins = new PinFunctionToolsSet(
            _pinRepository,
            _chatId,
            _gigaChatOptions.PinDuplicateDistance,
            _gigaChatOptions.PinSearchDistance);

        var tools = manualTools
            .Concat(pins.FunctionTools)
            .Where(tool => SectionFunctions.Contains(tool.Name))
            .ToList();

        return (tools, pins);
    }

    private async Task ReviewSectionAsync(
        ApplicationSectionDto section,
        int number,
        int total,
        string? userComment,
        string criteriaNames,
        CancellationToken ct)
    {
        Console.WriteLine(
            $"[REVIEWING]: {number}/{total} {Shorten(section.Title)} ({section.Content.Length} chars, " +
            $"сводка={_memo.Length} симв.)");

        var journal = await ReviewSectionAsync(
            section, number, total, userComment, criteriaNames, SectionToolCalls, MemoHeadChars, ct);

        await AbsorbAsync(section, journal, ct);
    }

    private async Task<IReadOnlyList<PinJournalEntry>> ReviewSectionAsync(
        ApplicationSectionDto section,
        int number,
        int total,
        string? userComment,
        string criteriaNames,
        int maxToolCalls,
        int memoBudget,
        CancellationToken ct)
    {
        var (tools, pins) = BuildSectionTools();

        try
        {
            await SendSectionAsync(
                section, number, total, userComment, criteriaNames, tools, maxToolCalls, memoBudget, ct);
        }
        catch (Exception ex) when (IsToolCallLimit(ex))
        {
            Console.WriteLine($"[REVIEW BUDGET]: {Shorten(section.Title)} -> лимит вызовов исчерпан, заметки сохранены");
        }
        catch (RequestEntityTooLargeError)
        {
            if (maxToolCalls > MinToolCallsPerSection)
            {
                var reduced = Math.Max(maxToolCalls / 2, MinToolCallsPerSection);
                Console.WriteLine($"[REVIEW TOO LARGE]: {Shorten(section.Title)} -> урезаю вызовы инструментов до {reduced}");

                return await ReviewSectionAsync(
                    section, number, total, userComment, criteriaNames, reduced, memoBudget, ct);
            }

            if (memoBudget > 0 && _memo.Length > 0)
            {
                var reduced = memoBudget / 2 >= MinMemoChars ? memoBudget / 2 : 0;
                Console.WriteLine($"[REVIEW TOO LARGE]: {Shorten(section.Title)} -> урезаю сводку до {reduced} симв.");

                return await ReviewSectionAsync(
                    section, number, total, userComment, criteriaNames, maxToolCalls, reduced, ct);
            }

            if (section.Content.Length <= MinSectionChars) throw;

            Console.WriteLine($"[REVIEW TOO LARGE]: {Shorten(section.Title)} ({section.Content.Length} chars) -> дроблю пополам");

            var collected = new List<PinJournalEntry>(pins.Journal);

            foreach (var half in SplitInHalf(section))
            {
                ct.ThrowIfCancellationRequested();

                collected.AddRange(await ReviewSectionAsync(
                    half, number, total, userComment, criteriaNames, SectionToolCalls, memoBudget, ct));
            }

            return collected;
        }

        return pins.Journal;
    }

    private async Task SendSectionAsync(
        ApplicationSectionDto section,
        int number,
        int total,
        string? userComment,
        string criteriaNames,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls,
        int memoBudget,
        CancellationToken ct)
    {
        var chat = new Chat
        {
            Messages =
            [
                Messages.System(SectionSystemPrompt(userComment, criteriaNames, MemoHead(memoBudget))),
                Messages.User($"""
                               ### ФРАГМЕНТ ЗАЯВКИ {number}/{total}

                               {section.Content}
                               """)
            ],
            FunctionCall = FunctionCallMode.Auto,
            Model = _gigaChatOptions.ApplicationReviewAgentModel
        };

        await _gigaChatClient.ChatWithToolsAsync(
            chat,
            tools,
            maxToolCalls: maxToolCalls,
            cancellationToken: ct);
    }

    private string MemoFor(int budget) =>
        budget <= 0 || _memo.Length == 0 ? string.Empty : Truncate(_memo, budget);

    private string MemoHead(int budget)
    {
        if (budget <= 0 || _memo.Length == 0) return string.Empty;
        if (_memo.Length <= budget) return _memo;

        var lines = SplitLines(_memo);
        var kept = new List<string>();
        var used = 0;

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = Truncate(lines[i], MemoLineChars);
            if (used + line.Length + 1 > budget) break;

            kept.Insert(0, line);
            used += line.Length + 1;
        }

        return kept.Count == 0 ? Truncate(_memo, budget) : string.Join("\n", kept);
    }

    private async Task AbsorbAsync(
        ApplicationSectionDto section,
        IReadOnlyList<PinJournalEntry> journal,
        CancellationToken ct)
    {
        var addition = BuildMemoAddition(section, journal);
        if (addition.Length == 0) return;

        var combined = _memo.Length == 0 ? addition : _memo + "\n" + addition;

        _memo = combined.Length <= MemoBudget
            ? combined
            : await CompactMemoAsync(combined, ct);
    }

    private static string BuildMemoAddition(ApplicationSectionDto section, IReadOnlyList<PinJournalEntry> journal)
    {
        if (journal.Count == 0) return string.Empty;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();

        foreach (var entry in journal)
        {
            var text = Truncate(entry.Content.ReplaceLineEndings(" ").Trim(), MemoLineChars);
            var key = NormalizeForCompare(text);

            if (key.Length == 0 || !seen.Add(key)) continue;

            lines.Add($"- [{entry.Type}] {text}");
        }

        return lines.Count == 0
            ? string.Empty
            : $"## {Shorten(section.Title)}\n{string.Join("\n", lines)}";
    }

    private async Task<string> CompactMemoAsync(string combined, CancellationToken ct)
    {
        var budget = MemoBudget;

        try
        {
            var chat = new Chat
            {
                Messages =
                [
                    Messages.System(MemoCompactionPrompt(budget)),
                    Messages.User(FitLines(SplitLines(combined), Budget))
                ],
                Model = _gigaChatOptions.ApplicationReviewAgentModel
            };

            var result = await SendWithRetryAsync(chat, "накопительная сводка", ct);
            var content = result.Choices.FirstOrDefault()?.Message.Content?.Trim() ?? string.Empty;

            if (content.Length == 0) return FitLines(SplitLines(combined), budget);

            Console.WriteLine($"[MEMO]: {combined.Length} -> {content.Length} симв.");

            return Truncate(content, budget);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEMO FAILED]: {GigaChatRetry.Describe(ex)} -> сжимаю сводку без модели");

            return FitLines(SplitLines(combined), budget);
        }
    }

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private async Task<string> BuildVerdictAsync(
        string criteria,
        string coverage,
        string? userComment,
        int reviewed,
        CancellationToken ct)
    {
        if (reviewed == 0) return ServiceUnavailableMessage();

        var pins = (await _pinRepository.GetPinsAsync(_chatId)).ToList();
        var findings = pins.Where(pin => VerdictPinOrder.Contains(pin.Type)).ToList();

        if (findings.Count == 0 && criteria.Length == 0)
        {
            return "## Итог\n\nПо методичке замечаний к заявке не найдено. " +
                   "Проверьте её глазами перед подачей — автоматическая проверка не заменяет эксперта.";
        }

        try
        {
            return await SendVerdictAsync(findings, criteria, coverage, userComment, Budget, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {

            Console.WriteLine($"[VERDICT FAILED]: {GigaChatRetry.Describe(ex)} -> отдаю заметки без итогового разбора");

            return "## Итог\n\nСобрать итоговый разбор не удалось — сервис ИИ не ответил. " +
                   "Ниже замечания, найденные при проверке; они также сохранены в этом чате, " +
                   "их можно обсудить обычным сообщением.\n\n" +
                   BuildPinsDigest(findings, FallbackDigestChars);
        }
    }

    private async Task<string> SendVerdictAsync(
        IReadOnlyList<Pin> findings,
        string criteria,
        string coverage,
        string? userComment,
        int digestBudget,
        CancellationToken ct)
    {
        var digest = BuildPinsDigest(findings, digestBudget);
        var memo = digestBudget > MinSectionChars ? MemoFor(MemoBudget) : string.Empty;

        var payload = memo.Length == 0
            ? digest
            : $"ЧТО ПРОЧИТАНО В ЗАЯВКЕ:\n{memo}\n\n{digest}";

        var chat = new Chat
        {
            Messages =
            [
                Messages.System(VerdictSystemPrompt(criteria, coverage, userComment)),
                Messages.User(payload)
            ],
            Model = _gigaChatOptions.ApplicationReviewAgentModel
        };

        try
        {
            var result = await SendWithRetryAsync(chat, "итоговый разбор", ct);

            Console.WriteLine(
                $"[VERDICT]: model={result.Model}, prompt_tokens={result.Usage?.PromptTokens}, " +
                $"заметок={findings.Count}, критерии={criteria.Length} симв., дайджест={digest.Length} симв., " +
                $"сводка={memo.Length} симв.");

            var content = result.Choices.FirstOrDefault()?.Message.Content;

            return string.IsNullOrWhiteSpace(content) ? digest : content;
        }
        catch (RequestEntityTooLargeError)
        {
            if (digestBudget > MinSectionChars)
            {
                var reduced = Math.Max(digestBudget / 2, MinSectionChars);
                Console.WriteLine($"[VERDICT TOO LARGE]: урезаю дайджест заметок до {reduced} симв.");

                return await SendVerdictAsync(findings, criteria, coverage, userComment, reduced, ct);
            }

            if (criteria.Length > 0)
            {
                Console.WriteLine("[VERDICT TOO LARGE]: собираю разбор без блока критериев.");
                return await SendVerdictAsync(findings, string.Empty, string.Empty, userComment, Budget, ct);
            }

            throw;
        }
    }

    private async Task<string> CheckCoverageAsync(
        IReadOnlyList<ApplicationSectionDto> sections,
        string criteria,
        CancellationToken ct)
    {
        if (criteria.Length == 0) return string.Empty;

        try
        {
            var outline = await BuildOutlineAsync(sections);
            if (outline.Length == 0) return string.Empty;

            var chat = new Chat
            {
                Messages =
                [
                    Messages.System(CoveragePrompt),
                    Messages.User($"КРИТЕРИИ:\n{criteria}\n\nРАЗДЕЛЫ ЗАЯВКИ:\n{outline}")
                ],
                Model = _gigaChatOptions.ApplicationReviewAgentModel
            };

            var result = await SendWithRetryAsync(chat, "сверка с критериями", ct);
            var content = result.Choices.FirstOrDefault()?.Message.Content?.Trim() ?? string.Empty;

            Console.WriteLine($"[COVERAGE]: оглавление={outline.Length} симв., ответ={content.Length} симв.");

            return content.StartsWith("НЕТ", StringComparison.OrdinalIgnoreCase) ? string.Empty : content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COVERAGE FAILED]: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<string> BuildOutlineAsync(IReadOnlyList<ApplicationSectionDto> sections)
    {
        var titles = sections
            .Select(section => section.Title)
            .Where(title => title.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(title => $"- {Shorten(title)}")
            .ToList();

        var outline = FitLines(titles, Budget);

        var evidence = _memo.Length > 0
            ? SplitLines(_memo)
            : (await _pinRepository.GetPinsAsync(_chatId, PinType.Summary))
                .Select(pin => $"- {Truncate(pin.Content, MaxPinContentChars)}")
                .ToList();

        if (evidence.Count == 0) return outline;

        return outline + "\n\nЧТО РЕАЛЬНО ПРОЧИТАНО В ЗАЯВКЕ:\n" + FitLines(evidence, Budget);
    }

    private static string FitLines(IReadOnlyList<string> lines, int budget)
    {
        if (lines.Count == 0) return string.Empty;

        var joined = string.Join("\n", lines);
        if (joined.Length <= budget) return joined;

        var perLine = budget / lines.Count - EllipsisReserve;
        if (perLine >= MinOutlineLineChars)
        {
            return string.Join("\n", lines.Select(line => Truncate(line, perLine)));
        }

        var keep = Math.Max(budget / (MinOutlineLineChars + EllipsisReserve) - 1, 1);
        var step = (int)Math.Ceiling(lines.Count / (double)keep);

        var sampled = new List<string>();
        for (var i = 0; i < lines.Count; i += step) sampled.Add(Truncate(lines[i], MinOutlineLineChars));
        if ((lines.Count - 1) % step != 0) sampled.Add(Truncate(lines[^1], MinOutlineLineChars));

        sampled.Add($"(в списке {sampled.Count} строк из {lines.Count}; отсутствие строки ничего не доказывает)");

        return string.Join("\n", sampled);
    }

    private async Task<string> ExtractCriteriaAsync(CancellationToken ct)
    {
        try
        {
            var parts = await _manualRepository.KnnSearchManualPartAsync(CriteriaQuery, CriteriaSearchLimit, _manualId);

            var source = string.Join("\n\n", parts.Select(part => $"{part.Title}\n{part.Content}"));
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;

            var chat = new Chat
            {
                Messages =
                [
                    Messages.System(CriteriaExtractionPrompt),
                    Messages.User(Truncate(source, Budget))
                ],
                Model = _gigaChatOptions.ApplicationReviewAgentModel
            };

            var result = await SendWithRetryAsync(chat, "критерии оценки", ct);
            var content = result.Choices.FirstOrDefault()?.Message.Content?.Trim() ?? string.Empty;

            Console.WriteLine($"[CRITERIA]: частей методички={parts.Count()}, ответ={content.Length} симв.");

            return content.StartsWith("НЕТ", StringComparison.OrdinalIgnoreCase) ? string.Empty : content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITERIA FAILED]: {ex.Message}");
            return string.Empty;
        }
    }
    
    private Task<ChatCompletion> SendWithRetryAsync(Chat chat, string operation, CancellationToken ct) =>
        GigaChatRetry.ExecuteAsync(
            token => _gigaChatClient.ChatAsync(chat, token),
            operation,
            _gigaChatOptions.MaxOperationAttempts,
            _gigaChatOptions.OperationRetryDelaySeconds,
            ct);

    private static List<Pin> Distinct(IReadOnlyList<Pin> pins)
    {
        var kept = new List<Pin>();

        foreach (var pin in pins)
        {
            var normalized = NormalizeForCompare(pin.Content);

            var duplicate = kept.Any(other =>
            {
                var existing = NormalizeForCompare(other.Content);

                if (existing.StartsWith(normalized, StringComparison.Ordinal) ||
                    normalized.StartsWith(existing, StringComparison.Ordinal))
                {
                    return true;
                }

                return Math.Min(existing.Length, normalized.Length) >= DuplicatePrefixChars &&
                       string.CompareOrdinal(existing, 0, normalized, 0, DuplicatePrefixChars) == 0;
            });

            if (!duplicate) kept.Add(pin);
        }

        return kept;
    }

    private static string NormalizeForCompare(string content) =>
        string.Join(' ', content.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string BuildPinsDigest(IReadOnlyList<Pin> findings, int budget)
    {
        var sb = new StringBuilder("Замечания, собранные при проверке заявки:\n");
        var omitted = 0;

        foreach (var type in VerdictPinOrder)
        {
            var ofType = Distinct(findings.Where(pin => pin.Type == type).ToList());
            if (ofType.Count == 0) continue;

            sb.AppendLine($"\n### {type} ({ofType.Count})");

            foreach (var pin in ofType)
            {
                var line = $"- {Truncate(pin.Content, MaxPinContentChars)}";

                if (sb.Length + line.Length > budget)
                {
                    omitted++;
                    continue;
                }

                sb.AppendLine(line);
            }
        }

        if (omitted > 0) sb.AppendLine($"\n(ещё {omitted} замечаний не поместились в разбор)");

        return sb.ToString();
    }

    private List<ApplicationSectionDto> PackSections(IReadOnlyList<ApplicationSectionDto> sections)
    {
        var packed = new List<ApplicationSectionDto>();
        var buffer = new StringBuilder();
        string? firstTitle = null;
        var merged = 0;

        void Flush()
        {
            if (buffer.Length == 0) return;

            var title = merged > 1 ? $"{firstTitle} (+{merged - 1})" : firstTitle!;
            packed.Add(new ApplicationSectionDto(title, buffer.ToString().TrimEnd()));

            buffer.Clear();
            firstTitle = null;
            merged = 0;
        }

        foreach (var section in sections)
        {
            if (section.Content.Length > Budget)
            {
                Flush();
                packed.AddRange(SplitToBudget(section));
                continue;
            }

            if (buffer.Length + section.Content.Length > Budget) Flush();

            firstTitle ??= section.Title;
            merged++;

            buffer.AppendLine($"## {section.Title}");
            buffer.AppendLine(section.Content);
            buffer.AppendLine();
        }

        Flush();

        return packed;
    }

    private IEnumerable<ApplicationSectionDto> SplitToBudget(ApplicationSectionDto section)
    {
        var chunks = TextSplitter.SplitIntoStructuralChunks(section.Content, Budget, Budget / 4);

        return chunks.Select((chunk, i) => new ApplicationSectionDto(
            $"{section.Title} (часть {i + 1}/{chunks.Count})",
            $"## {section.Title}\n{chunk}"));
    }

    private static IEnumerable<ApplicationSectionDto> SplitInHalf(ApplicationSectionDto section)
    {
        var chunks = TextSplitter.SplitIntoNParts(section.Content, 2);

        return chunks.Select((chunk, i) =>
            new ApplicationSectionDto($"{section.Title} ({i + 1}/{chunks.Count})", chunk));
    }

    private static string ServiceUnavailableMessage() =>
        "## Проверка не выполнена\n\n" +
        "Сервис ИИ отвечает ошибками — ни один фрагмент заявки проверить не удалось. " +
        "Это сбой на нашей стороне, а не проблема с документом: загрузите заявку ещё раз " +
        "через несколько минут.\n\n" +
        "Разбора за этим сообщением нет — не считайте его признаком того, что с заявкой всё в порядке.";

    private static string FailureNote(IReadOnlyList<string> failed, int total)
    {
        var shown = string.Join("; ", failed.Take(MaxReportedFailures).Select(Shorten));
        var tail = failed.Count > MaxReportedFailures ? $" и ещё {failed.Count - MaxReportedFailures}" : string.Empty;

        return "\n\n---\n" +
               $"⚠️ Не удалось проверить {failed.Count} фрагмент(ов) из {total}: {shown}{tail}. " +
               "Их стоит просмотреть вручную.";
    }

    private static string SectionSystemPrompt(string? userComment, string criteriaNames, string memo) => $"""
Ты проверяешь один фрагмент заявки на грант по методичке. Ответ пользователю не пишешь — только
складываешь находки в заметки, разбор соберут отдельным шагом.
{CriteriaNamesBlock(criteriaNames)}{MemoBlock(memo)}
Как работать:
1. search_in_manual — требования методички к этому фрагменту, хватает одного-двух запросов.
2. create_pin — записать находку. Ответ Duplicate значит, что наблюдение уже есть, его пропускай.
3. Ровно одна заметка Summary на фрагмент — о чём он.
4. Записал существенное — заканчивай. Пустой ход лучше лишней заметки.

search_pins — поднять свои прежние заметки, если фрагмент ссылается на то, чего ты не видел.
update_pin и delete_pin — поправить или убрать прежнюю заметку, но только если этот фрагмент её
действительно опроверг.

Ты видишь один фрагмент из многих, следующие ещё не читал никто. Поэтому вывод «в заявке этого
нет» тебе недоступен — такая заметка автоматически станет WhatToCheck. Ошибку фиксируй, только
показав, что во фрагменте НАПИСАНО и какому требованию методички это противоречит.

Теги вида [GIVEN_NAME_0001] — вырезанные персональные данные. Поле с тегом считается заполненным,
претензии к нему запрещены: это ограничение проверки, а не недостаток заявки.

Требования бери только из методички. Отсутствие замечаний — нормальный результат.
{UserCommentBlock(userComment)}
""";

    private static string MemoCompactionPrompt(int budget) => $"""
Ты ведёшь накопительную сводку по заявке на грант: что в ней уже прочитано и что найдено.
Тебе дана текущая сводка и наблюдения из очередного фрагмента, дописанные в конец.

Собери из них одну обновлённую сводку.

Правила:
- Наблюдения об одном и том же объединяй в одну строку, дополняя её, а не повторяя.
- Ничего не выбрасывай целиком: каждый прочитанный раздел заявки должен остаться упомянут хотя бы
  несколькими словами. Сокращай формулировки, но не выкидывай строки — по этой сводке потом решают,
  чего в заявке нет, и пропавшая строка читается как отсутствующий раздел.
- Формат — маркированный список, по строке на факт. Без заголовков, вступлений и выводов.
- Только то, что есть во входных данных. Ничего не додумывай.
- Уложись в {budget} символов.
""";

    private const string CriteriaExtractionPrompt = """
Тебе даны фрагменты методички по грантам. Выпиши из них критерии, по которым оценивается заявка.

Формат ответа — по одной строке на критерий:
Название критерия — что именно оценивается (одна короткая фраза).

Правила:
- Не больше 10 критериев, только те, что действительно есть в тексте.
- Если в тексте указана шкала оценки (баллы, диапазоны, вес критерия), добавь последней строкой:
  ШКАЛА: <как оценивают>.
- Ничего не добавляй от себя и не пересказывай требования к оформлению.
- Если критериев оценки в тексте нет — ответь одним словом: НЕТ.
""";

    private const string CoveragePrompt = """
Тебе дан список критериев оценки заявки на грант, оглавление заявки и краткие выжимки того, что
в ней реально прочитано.

Определи, для каких критериев в заявке нет вообще ничего подходящего.

Формат ответа — по строке на критерий:
Название критерия — подходящего раздела в заявке нет.

Правила:
- Презумпция в пользу заявки: заявка — это форма, её разделы называются по-своему и почти никогда
  не совпадают с формулировкой критерия. «Количество просмотров» относится к информационной
  открытости, «Роль в проекте» — к опыту команды, строки со сметой и суммами — к расходам и бюджету.
- Считай критерий покрытым, если к нему по смыслу относится хоть один раздел или хоть одна выжимка.
- Выноси критерий в ответ, только если ты уверен, что в заявке нет ничего похожего. Сомневаешься —
  считай покрытым и не выноси.
- Про качество и полноту заполнения не рассуждай, только про наличие.
- Если все критерии чем-то покрыты — ответь одним словом: НЕТ.
""";

    private static string VerdictSystemPrompt(string criteria, string coverage, string? userComment) => $"""
Ты — эксперт, проверивший заявку на грант по методичке. В сообщении пользователя два блока:
ЧТО ПРОЧИТАНО В ЗАЯВКЕ — сводка того, что в заявке было, она собиралась по мере чтения и показывает,
какие разделы в заявке есть; ниже — замечания, зафиксированные при проверке. Разбор строй по
замечаниям, а сводку используй как доказательство наличия разделов: если раздел упомянут в сводке,
писать, что его нет, нельзя. Собери итоговый разбор в Markdown.
{CriteriaBlock(criteria)}{CoverageBlock(coverage)}
Структура разбора:
{(criteria.Length > 0
    ? """
      ## Оценка по критериям — по одному пункту на каждый критерий из блока КРИТЕРИИ ОЦЕНКИ.
         Для каждого: название критерия, вывод и обоснование ссылкой на конкретные замечания.
         Вывод по критерию: «соответствует», «частично соответствует» или «не соответствует».
         Если методичка задаёт шкалу в баллах — выстави балл по этой шкале и обоснуй его.
         Критерий из блока ПРОПУЩЕННЫЕ РАЗДЕЛЫ — «не соответствует», раздела в заявке нет.
         Если по критерию замечаний не зафиксировано — так и напиши: «замечаний не зафиксировано».
         Не выдавай отсутствие замечаний за подтверждённое соответствие и не занижай за это балл.
      """
    : "")}
## Критические ошибки — из категории Mistake: в чём ошибка и что исправить.
## Спорные моменты — из категории Attention.
## Что стоит проверить — из категории WhatToCheck.
## Итог — 2-4 предложения: можно ли подавать заявку и что исправить в первую очередь.

Правила:
- Только то, что есть в замечаниях. Ничего не добавляй "по смыслу".
- Строки сводки — это не замечания. Не переписывай их в разделы разбора как недостатки.
- Критерии бери только из блока КРИТЕРИИ ОЦЕНКИ, своих не выдумывай.
- Категории без замечаний — напиши, что замечаний нет, и не рассуждай о грантах вообще.
- Теги вида [GIVEN_NAME_0001], [EMAIL_00D9] — это персональные данные, вырезанные из заявки до
  проверки. В документе на их месте стоят настоящие значения. Ставить из-за них «не соответствует»,
  писать «данные не указаны» или «невозможно проверить достоверность» запрещено: это ограничение
  проверки, а не недостаток заявки. Если вывод по критерию упирается в такие теги, так и скажи:
  «Эти данные от меня скрыты, я могу оценить лишь наличие этого раздела — оно есть, и это уже плюс».
- Писать, что чего-то в заявке нет, можно ТОЛЬКО про то, что перечислено в блоке ПРОПУЩЕННЫЕ
  РАЗДЕЛЫ. Замечания проверки писались по одному фрагменту заявки за раз, поэтому вывод об
  отсутствии из них не следует: раздел почти наверняка есть в другой части заявки.
- Замечания со словами «не указано», «отсутствует», «нет информации» игнорируй, если тот же пункт
  не подтверждён блоком ПРОПУЩЕННЫЕ РАЗДЕЛЫ.
{UserCommentBlock(userComment)}
""";

    private static string CriteriaNamesBlock(string criteriaNames) =>
        criteriaNames.Length == 0
            ? string.Empty
            : $"""

               Заявку в итоге оценят по критериям: {criteriaNames}
               Если фрагмент относится к какому-то из них, назови этот критерий в начале заметки
               обычным текстом, без скобок.

               """;

    private static string MemoBlock(string memo) =>
        memo.Length == 0
            ? string.Empty
            : $"""

               ПРОЧИТАНО РАНЬШЕ (хвост сводки; полная версия уйдёт в итоговый разбор):
               {memo}
               Сводка ведётся автоматически и не правится: удаление заметки её не стирает.

               """;

    private static string CoverageBlock(string coverage) =>
        coverage.Length == 0
            ? string.Empty
            : $"""

               ПРОПУЩЕННЫЕ РАЗДЕЛЫ (сверка оглавления заявки с критериями):
               {coverage}

               """;

    private static string CriteriaBlock(string criteria) =>
        criteria.Length == 0
            ? string.Empty
            : $"""

               КРИТЕРИИ ОЦЕНКИ из методички:
               {criteria}

               """;

    private static string UserCommentBlock(string? userComment) =>
        string.IsNullOrWhiteSpace(userComment)
            ? string.Empty
            : $"""

               Пользователь просил обратить особое внимание на следующее:
               {userComment.Trim()}
               """;

    private static bool IsToolCallLimit(Exception ex) =>
        ex.Message.Contains(ToolCallLimitMarker, StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";

    private static string Shorten(string title) => Truncate(title, 60);
}
