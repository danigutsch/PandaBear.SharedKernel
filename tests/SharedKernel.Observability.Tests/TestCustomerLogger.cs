using Microsoft.Extensions.Logging;

namespace PandaBear.Observability.Tests;

/// <summary>Source-generated log method fixture whose arguments are marked as personal, credential, sensitive and financial data.</summary>
internal static partial class TestCustomerLogger
{
    [LoggerMessage(1, LogLevel.Information, "Imported customer {Email} using {Credential}; sensitive {SensitiveValue}; financial {FinancialValue}; outcome {Outcome}.")]
    internal static partial void LogImportedCustomer(
        ILogger logger,
        [PersonalData] string email,
        [CredentialData] string credential,
        [SensitiveData] string sensitiveValue,
        [FinancialData] string financialValue,
        string outcome);
}
