namespace SharedKernel.CI.Tests;

/// <summary>Guards how a local check recognizes a sandbox that refuses Unix domain sockets.</summary>
public sealed class SocketRefusalTests
{
    [Theory]
    [InlineData("System.Net.Sockets.SocketException (13): Permission denied", true)]
    [InlineData("connect to the pipe: Permission denied", true)]
    [InlineData("Unix socket Permission denied", true)]
    [InlineData("error: Permission denied reading /etc/shadow", false)]
    [InlineData("System.Net.Sockets.SocketException (111): Connection refused", false)]
    [InlineData("System.Net.Sockets.SocketException (1): Operation not permitted", true)]
    [InlineData("/home/me/.nuget/Pipelines/Sockets.Unofficial.dll: Permission denied", false)]
    [InlineData("Permission denied: /home/me/.nuget/Pipelines/Sockets.Unofficial.dll", false)]
    [InlineData("error CS0246: type not found", false)]
    public void Socket_refusal_is_recognized_by_the_exception_or_by_permission_denied_next_to_a_socket(string output, bool refused) =>
        SocketRefusal.IsRefused(output).ShouldBe(refused);
}
