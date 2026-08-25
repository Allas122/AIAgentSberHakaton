using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Infrastructure.Mappers;

[Mapper]
public static partial class UserAccountMapper
{
    [MapProperty(nameof(UserAccount.PasswordHash), nameof(User.HashedPassword))]
    [MapperIgnoreSource(nameof(UserAccount.Login))]
    [MapperIgnoreSource(nameof(UserAccount.CreatedAt))]
    [MapperIgnoreSource(nameof(UserAccount.UpdatedAt))]
    public static partial User MapToUser(this UserAccount account);
}
