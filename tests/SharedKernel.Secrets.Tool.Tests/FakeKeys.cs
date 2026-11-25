namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Text shaped like age secret keys, which no key generator produced.</summary>
internal static class FakeKeys
{
    // Split in two so that secret scanners, which match the whole key shape, do not report this fake key.

    /// <summary>A well-formed age secret key made of the letter Q.</summary>
    public const string Secret = "AGE-SECRET-KEY-" + "1QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ";

    /// <summary>A well-formed age secret key that uses every Bech32 character.</summary>
    public const string Varied = "AGE-SECRET-KEY-" + "1QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7LQPZRY9X8GF2TVDW0S3JN54KHCE";
}
