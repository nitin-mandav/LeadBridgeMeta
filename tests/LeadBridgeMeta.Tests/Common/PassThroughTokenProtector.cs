using LeadBridgeMeta.Application.Common;

namespace LeadBridgeMeta.Tests.Common;

/// <summary>No-op stand-in for the real Data Protection-backed protector, so unit tests can just store
/// plaintext tokens without pulling in ASP.NET Core's data protection stack.</summary>
public class PassThroughTokenProtector : ITokenProtector
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string ciphertext) => ciphertext;
}
