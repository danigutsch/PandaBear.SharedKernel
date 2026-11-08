namespace PandaBear.EntityFrameworkCore.Analyzers.Tests;

/// <summary>Builds EF Core model configuration sources for the unique index analyzer tests.</summary>
internal static class UniqueIndexSources
{
    /// <summary>Wraps statements in an <c>OnModelCreating</c> override that configures <c>Customer</c> through <c>builder</c>.</summary>
    public static string InOnModelCreating(string statements) => $$"""
        namespace Demo;

        public class Customer
        {
            public int Id { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public class Db : DbContext
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var builder = modelBuilder.Entity<Customer>();
                {{statements}}
            }
        }
        """;

    /// <summary>Wraps statements in an <c>IEntityTypeConfiguration&lt;Customer&gt;.Configure</c> method.</summary>
    public static string InConfiguration(string statements) => $$"""
        namespace Demo;

        public class Customer
        {
            public int Id { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
        {
            public void Configure(EntityTypeBuilder<Customer> builder)
            {
                {{statements}}
            }
        }
        """;

    /// <summary>A customer entity that carries the given attribute line above the class.</summary>
    public static string WithClassAttribute(string attribute) => $$"""
        namespace Demo;

        {{attribute}}
        public class Customer
        {
            public int Id { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
        """;
}
