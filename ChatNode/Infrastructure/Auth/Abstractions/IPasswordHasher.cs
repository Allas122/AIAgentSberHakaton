namespace ChatNode.Infrastructure.Auth.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string stored);

    bool NeedsRehash(string stored);
}
