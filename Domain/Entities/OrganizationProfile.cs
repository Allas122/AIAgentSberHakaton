namespace Domain.Entities;

public record OrganizationProfile
{
    public Guid OwnerId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;

    public string Okpo { get; set; } = string.Empty;
    public string Ogrn { get; set; } = string.Empty;
    public string Inn { get; set; } = string.Empty;
    public string Kpp { get; set; } = string.Empty;

    public string SignerPosition { get; set; } = string.Empty;

    public string SignerName { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;
    public string ContactPosition { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public string ExecutorName { get; set; } = string.Empty;
    public string ExecutorPhone { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
