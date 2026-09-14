namespace LeadBridgeMeta.Application.Common;

/// <summary>Encrypts/decrypts long-lived third-party access & refresh tokens before they touch the database.</summary>
public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
