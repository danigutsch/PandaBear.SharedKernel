using Microsoft.EntityFrameworkCore;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A second empty test context, used to show that registrations are per context type.</summary>
/// <param name="options">The options that configure the context.</param>
internal sealed class OtherTestDbContext(DbContextOptions<OtherTestDbContext> options) : DbContext(options);
