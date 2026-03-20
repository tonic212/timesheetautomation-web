namespace TimesheetAutomation.Web.Services;

public interface ITwoFactorService
{
    string GenerateNewSecretKey();

    string BuildManualEntryKey(string secretKey);

    string BuildAuthenticatorUri(string email, string secretKey);

    string BuildQrCodeDataUri(string authenticatorUri);

    bool ValidateCode(string secretKey, string code);

    string[] GenerateRecoveryCodes(int count);

    bool ValidateRecoveryCode(string[] existingCodes, string code, out string[] remainingCodes);
}