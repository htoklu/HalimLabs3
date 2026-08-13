using HalimLabs.Configuration;
using HalimLabs.Services.Abstractions;

namespace HalimLabs.Services.Support;

public sealed class SupportInfoProvider : ISupportInfoProvider
{
    public SupportInfoProvider()
    {
        Current = new SupportOptions
        {
            DeveloperName = SupportConstants.DeveloperName,
            FooterText = SupportConstants.FooterText,
            SupportText = SupportConstants.SupportText,
            UsdtAddress = SupportConstants.UsdtAddress,
            KofiUrl = SupportConstants.KofiUrl,
            Iban = SupportConstants.Iban,
            IbanHolder = SupportConstants.IbanHolder,
            BankName = SupportConstants.BankName
        };
    }

    public SupportOptions Current { get; }
}
