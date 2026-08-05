namespace ChatNode.Infrastructure.AI.Functions.ReturnModels;

public record GetPinsReturn(string Status, int Count, IReadOnlyList<PinView> Pins);
