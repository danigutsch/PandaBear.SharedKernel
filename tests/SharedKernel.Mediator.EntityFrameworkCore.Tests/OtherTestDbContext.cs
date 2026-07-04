using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Mediator.EntityFrameworkCore.Tests;

internal sealed class OtherTestDbContext(DbContextOptions<OtherTestDbContext> options) : DbContext(options);
