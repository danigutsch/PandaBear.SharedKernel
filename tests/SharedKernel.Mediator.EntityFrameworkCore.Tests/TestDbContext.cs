using Microsoft.EntityFrameworkCore;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>An empty context backed by the in-memory provider, used as the transaction target in tests.</summary>
/// <param name="options">The options that configure the context.</param>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
