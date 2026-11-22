using System;
using System.Security.Cryptography;

namespace XSphere.Services;

public static class ProfileSecurityService
{
    public const int DefaultIterations = 600_000;

    public static bool IsValidPin(string? pin)
    {
        if (pin is null || pin.Length < 4 || pin.Length > 8)
            return false;

        foreach (char character in pin)
        {
            if (character < '0' || character > '9')
                return false;
        }

        return true;
    }

    public static (string Hash, string Salt, int Iterations)
        HashPin(string pin)
    {
        if (!IsValidPin(pin))
            throw new ArgumentException(
                "PIN must contain 4 to 8 digits.",
                nameof(pin));

        byte[] salt = RandomNumberGenerator.GetBytes(32);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            32);

        return (
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            DefaultIterations);
    }

    public static bool VerifyPin(
        string? pin,
        string? storedHash,
        string? storedSalt,
        int iterations)
    {
        if (!IsValidPin(pin) ||
            string.IsNullOrEmpty(storedHash) ||
            string.IsNullOrEmpty(storedSalt) ||
            iterations < 100_000 ||
            iterations > 2_000_000)
        {
            return false;
        }

        try
        {
            byte[] expectedHash =
                Convert.FromBase64String(storedHash);

            byte[] salt =
                Convert.FromBase64String(storedSalt);

            if (expectedHash.Length != 32 || salt.Length != 32)
                return false;

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                pin!,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
