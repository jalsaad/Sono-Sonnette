using System.Security.Cryptography;
using SonoSonnette.Core.Models;

namespace SonoSonnette.Core.Services;

/// <summary>Hachage/vérification du mot de passe protégeant la modification des paramètres (PBKDF2-SHA256).</summary>
public static class PasswordService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static void SetPassword(AppSettings settings, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        settings.PasswordSalt = Convert.ToBase64String(salt);
        settings.PasswordHash = Convert.ToBase64String(hash);
    }

    public static bool HasPassword(AppSettings settings) =>
        !string.IsNullOrEmpty(settings.PasswordHash) && !string.IsNullOrEmpty(settings.PasswordSalt);

    public static bool Verify(AppSettings settings, string password)
    {
        if (!HasPassword(settings))
        {
            return false;
        }

        var salt = Convert.FromBase64String(settings.PasswordSalt!);
        var expected = Convert.FromBase64String(settings.PasswordHash!);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
