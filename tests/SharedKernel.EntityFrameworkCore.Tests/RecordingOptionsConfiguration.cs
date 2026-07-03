using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class RecordingOptionsConfiguration(List<string> calls, string name) : IDbContextOptionsConfiguration<TestDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        calls.Add(name);
    }
}
