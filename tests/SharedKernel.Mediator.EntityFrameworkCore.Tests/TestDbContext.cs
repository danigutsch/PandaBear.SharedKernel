using Microsoft.EntityFrameworkCore;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
