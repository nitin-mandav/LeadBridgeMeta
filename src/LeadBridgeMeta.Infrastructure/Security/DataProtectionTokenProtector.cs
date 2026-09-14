using LeadBridgeMeta.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace LeadBridgeMeta.Infrastructure.Security;

public class DataProtectionTokenProtector : ITokenProtector
{
    private const string Purpose = "LeadBridgeMeta.ThirdPartyTokens.v1";
    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
