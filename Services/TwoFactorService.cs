using OtpNet;
using QRCoder;

namespace TimesheetAutomation.Web.Services;

public sealed class TwoFactorService : ITwoFactorService
{
    private const string Issuer = "TimesheetAutomation";

    public string GenerateNewSecretKey()
    {
        byte[] key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string BuildManualEntryKey(string secretKey)
    {
        return secretKey;
    }

    public string BuildAuthenticatorUri(string email, string secretKey)
    {
        string encodedIssuer = Uri.EscapeDataString(Issuer);
        string encodedEmail = Uri.EscapeDataString(email);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&digits=6";
    }

    public string BuildQrCodeDataUri(string authenticatorUri)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrCodeData = generator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new(qrCodeData);
        byte[] pngBytes = qrCode.GetGraphic(8);

        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    public bool ValidateCode(string secretKey, string code)
    {
        string normalizedCode = NormalizeCode(code);
        if (normalizedCode.Length != 6 || !normalizedCode.All(char.IsDigit))
        {
            return false;
        }

        byte[] keyBytes = Base32Encoding.ToBytes(secretKey);
        Totp totp = new(keyBytes);

        return totp.VerifyTotp(normalizedCode, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    public string[] GenerateRecoveryCodes(int count)
    {
        List<string> codes = new(count);

        for (int i = 0; i < count; i++)
        {
            string code = $"{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            codes.Add(code);
        }

        return codes.ToArray();
    }

    public bool ValidateRecoveryCode(string[] existingCodes, string code, out string[] remainingCodes)
    {
        string normalized = NormalizeCode(code).ToUpperInvariant();

        List<string> codes = existingCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .ToList();

        if (!codes.Contains(normalized))
        {
            remainingCodes = existingCodes;
            return false;
        }

        codes.Remove(normalized);
        remainingCodes = codes.ToArray();
        return true;
    }

    private static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : code.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
    }
}