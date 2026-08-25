using System.Security.Cryptography;
using System.Text;
using ChatNode.Infrastructure.Auth.Abstractions;

namespace ChatNode.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return false;

        var parts = stored.Split('$');

        if (parts.Length != 4 || parts[0] != Prefix) return VerifyLegacySha256(password, stored);

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool NeedsRehash(string stored) =>
        !stored.StartsWith($"{Prefix}${Iterations}$", StringComparison.Ordinal);

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashBytes);

    private static bool VerifyLegacySha256(string password, string stored)
    {
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual),
            Encoding.ASCII.GetBytes(stored.Trim().ToUpperInvariant()));
    }
}
