using Microsoft.EntityFrameworkCore;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

internal sealed class OtherTestDbContext(DbContextOptions<OtherTestDbContext> options) : DbContext(options);
