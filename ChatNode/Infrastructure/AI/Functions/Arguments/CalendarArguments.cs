namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record CurrentDateArguments;

public record ResolveDateArguments
(
    string Anchor,
    int? Offset,
    string? From
);

public record DateDifferenceArguments
(
    string To,
    string? From
);
