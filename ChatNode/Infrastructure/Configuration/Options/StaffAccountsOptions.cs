using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Configuration.Options;

public class StaffAccountsOptions
{
    public List<StaffAccountOption> Accounts { get; set; } = [];
}

public class StaffAccountOption
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}
