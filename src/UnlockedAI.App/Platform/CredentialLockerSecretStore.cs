using UnlockedAI.Core.Secrets;
using Windows.Security.Credentials;

namespace UnlockedAI.Platform;

/// <summary>
/// Stores secrets in the Windows Credential Locker, which encrypts them for the signed-in user.
/// They show up in Windows' Credential Manager under "UnlockedAI".
/// </summary>
internal sealed class CredentialLockerSecretStore : ISecretStore
{
    private const string Resource = "UnlockedAI";

    // What the locker reports when there is no such entry.
    private const int ElementNotFound = unchecked((int)0x80070490);

    public string? Read(string name)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, name);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception exception)
        {
            if (exception.HResult != ElementNotFound)
            {
                CrashLog.Write(exception);
            }

            return null;
        }
    }

    public void Write(string name, string? value)
    {
        var vault = new PasswordVault();

        try
        {
            vault.Remove(vault.Retrieve(Resource, name));
        }
        catch (Exception exception) when (exception.HResult == ElementNotFound)
        {
            // Nothing stored yet, so nothing to replace.
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            vault.Add(new PasswordCredential(Resource, name, value.Trim()));
        }
    }
}
