using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Mediator.EntityFrameworkCore.Tests;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
