using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class ManualService(
    AgentFactory agentFactory,
    IManualRepository manualRepository,
    IFileStorage fileStorage
    ) : IManualService
{
    public async Task<string> ProcessManual(string title,Stream fileStream, CancellationToken cancellationToken)
    {
        var manualId = await manualRepository.CreateManualAsync(new (Guid.NewGuid(), title, ""));

        using var reader = new StreamReader(fileStream, leaveOpen: true);
        string textContent = await reader.ReadToEndAsync(cancellationToken);
        if (fileStream.CanSeek) fileStream.Position = 0;

        await fileStorage.UploadAsync(StorageKeys.Manual(manualId), fileStream, cancellationToken);

        ManualParserAgent manualParserAgent = agentFactory.CreateManualParserAgent(manualId);
        string result = await manualParserAgent.InvokeAsync(textContent ,cancellationToken);
        return result;
    }

    public async Task<IEnumerable<ManualDto>> GetManuals()
    {
        var ids = await manualRepository.GetManualIdsAsync();
        
        var tasks = ids.Select(id => manualRepository.GetManualAsync(id));
    
        var results = await Task.WhenAll(tasks);
        
        return results
            .Where(m => m != null)
            .Select(m => new ManualDto 
            { 
                Id = m!.Id, 
                Title = m.Title 
            });
    }
}