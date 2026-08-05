namespace Domain.Entities;

public enum PinType
{
    WhatToCheck = 0,
    Summary = 1,
    Mistake = 2,
    Attention = 3,
}

public record Pin(Guid Id, Guid SessionId, string Content, PinType Type, DateTimeOffset CreatedAt);

public record PinMatch(Pin Pin, double Distance);
