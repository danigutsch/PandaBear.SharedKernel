using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

internal static partial class TestCustomerLogger
{
    [LoggerMessage(1, LogLevel.Information, "Imported customer {Email}.")]
    internal static partial void LogImportedCustomer(ILogger logger, [TestPersonalData] string email);
}
