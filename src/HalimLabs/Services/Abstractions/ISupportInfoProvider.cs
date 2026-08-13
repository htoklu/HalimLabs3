using HalimLabs.Configuration;

namespace HalimLabs.Services.Abstractions;

public interface ISupportInfoProvider
{
    SupportOptions Current { get; }
}
