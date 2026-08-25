namespace Domain.ValueTypes;

public enum ManualStage
{
    Queued = 0,
    Parsing = 1,
    Ready = 2,
    Partial = 3,
    Failed = 4
}
