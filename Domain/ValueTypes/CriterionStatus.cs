namespace Domain.ValueTypes;

public enum CriterionStatus
{
    NoIssues = 0,
    Questionable = 1,
    Violated = 2,
    NotFound = 3,
    NotChecked = 4
}

public enum FindingSource
{
    Model = 0,
    Computed = 1,
    Coverage = 2
}

public enum FindingScope
{
    Fragment = 0,
    Document = 1
}
