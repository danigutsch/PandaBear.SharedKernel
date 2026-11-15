using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Testing.SourceGenerator;

namespace PandaBear.Testing.GeneratorTests;

/// <summary>Tests the diagnostics the CallProxy generator reports for a service it cannot proxy.</summary>
public sealed class CallProxyGeneratorDiagnosticTests
{
    [Theory]
    [InlineData("CallProxy.Create<Clock>(new Clock())", "Clock", "Clock")]
    [InlineData("CallProxy.Create(new Clock())", "CallProxy.Create(new Clock())", "Clock")]
    [InlineData("new ServiceCollection().WrapWithCallProxy<Clock>(_ => { })", "Clock", "Clock")]
    [InlineData("CallProxy.Create<Holder>(null!)", "Holder", "Holder")]
    public void A_service_that_is_not_an_interface_is_reported_where_it_is_named(string call, string located, string service)
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation($$"""
            namespace Demo;

            public class Clock { }

            public abstract class Holder { }

            public static class Sample
            {
                public static void Wrap() => {{call}};
            }
            """);

        // Act
        var diagnostic = GeneratorTestHarness.GetDiagnostics(GeneratorTestHarness.Run(compilation)).ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe(CallProxyDiagnosticIds.NotAnInterface);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        GeneratorTestHarness.GetLocatedText(compilation, diagnostic).ShouldBe(located);
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe($"CallProxy can only wrap an interface; '{service}' is not an interface");
    }

    [Fact]
    public void A_hook_on_the_hooks_of_a_class_is_reported_at_the_hooked_member()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation("""
            namespace Demo;

            public class Clock { }

            public static class Sample
            {
                public static void Stub(CallHooks<Clock> hooks) => hooks.Now.Before(_ => { });
            }
            """);

        // Act
        var diagnostic = GeneratorTestHarness.GetDiagnostics(GeneratorTestHarness.Run(compilation)).ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe(CallProxyDiagnosticIds.NotAnInterface);
        GeneratorTestHarness.GetLocatedText(compilation, diagnostic).ShouldBe("hooks.Now");
    }

    [Theory]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("private protected")]
    public void An_interface_nested_out_of_reach_of_the_assembly_is_reported(string accessibility)
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation($$"""
            namespace Demo;

            public class Holder
            {
                {{accessibility}} interface IClock { void Tick(); }

                internal static void Wrap(object clock) => CallProxy.Create<IClock>((IClock)clock);
            }
            """);

        // Act
        var diagnostic = GeneratorTestHarness.GetDiagnostics(GeneratorTestHarness.Run(compilation)).ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe(CallProxyDiagnosticIds.InaccessibleInterface);
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe("CallProxy cannot implement 'Holder.IClock', which is not accessible from this assembly");
    }

    [Theory]
    [InlineData("internal interface IClock { void Tick(); }", "IClock")]
    [InlineData("internal class Holder { internal interface IClock { void Tick(); } }", "Holder.IClock")]
    [InlineData("public class Holder { public interface IClock { void Tick(); } }", "Holder.IClock")]
    public void An_internal_or_public_interface_is_proxied(string declaration, string service)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            {{declaration}}

            internal static class Sample
            {
                internal static {{service}} Wrap({{service}} clock) => CallProxy.Create(clock, hooks => hooks.Tick.Before(_ => { }));
            }
            """;

        // Act
        var diagnostics = GeneratorTestHarness.Compile(source, out var runResult);

        // Assert
        diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.GetHintNames(runResult).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("string Name { get; }", "IStore.Name", "a property")]
    [InlineData("string this[int index] { get; }", "IStore.this[int]", "an indexer")]
    [InlineData("event EventHandler Changed;", "IStore.Changed", "an event")]
    [InlineData("Task<T> Get<T>(Guid id);", "IStore.Get<T>(Guid)", "a generic method")]
    [InlineData("bool TryGet(string key, out string? value);", "IStore.TryGet(string, out string?)", "a method with a ref, out or in parameter")]
    [InlineData("void Increment(ref int value);", "IStore.Increment(ref int)", "a method with a ref, out or in parameter")]
    [InlineData("void Read(in Guid id);", "IStore.Read(in Guid)", "a method with a ref, out or in parameter")]
    [InlineData("ref int Slot();", "IStore.Slot()", "a method that returns by reference")]
    [InlineData("int Sum(ReadOnlySpan<int> values);", "IStore.Sum(ReadOnlySpan<int>)", "a method with a ref struct or pointer parameter or result")]
    public void A_member_without_typed_hooks_is_reported_and_nothing_is_generated(string member, string display, string reason)
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation($$"""
            namespace Demo;

            public interface IStore
            {
                Task Save();

                {{member}}
            }

            public static class Sample
            {
                public static IStore Wrap(IStore store) => CallProxy.Create<IStore>(store);
            }
            """);

        // Act
        var runResult = GeneratorTestHarness.Run(compilation);

        // Assert
        GeneratorTestHarness.GetHintNames(runResult).ShouldBeEmpty();
        var diagnostic = GeneratorTestHarness.GetDiagnostics(runResult).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(CallProxyDiagnosticIds.UnsupportedMember);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe($"CallProxy cannot wrap 'IStore': its member '{display}' is {reason}");
        GeneratorTestHarness.GetLocatedText(compilation, diagnostic).ShouldBe("IStore");
    }

    [Fact]
    public void Every_unsupported_member_of_an_inherited_interface_is_reported()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation("""
            namespace Demo;

            public interface IBase
            {
                string Name { get; }
            }

            public interface IStore : IBase
            {
                event EventHandler Changed;
            }

            public static class Sample
            {
                public static IStore Wrap(IStore store) => CallProxy.Create<IStore>(store);
            }
            """);

        // Act
        var diagnostics = GeneratorTestHarness.GetDiagnostics(GeneratorTestHarness.Run(compilation));

        // Assert
        diagnostics.Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ShouldBe(
        [
            "CallProxy cannot wrap 'IStore': its member 'IBase.Name' is a property",
            "CallProxy cannot wrap 'IStore': its member 'IStore.Changed' is an event",
        ]);
    }

    [Fact]
    public void A_project_before_the_language_version_with_extension_members_is_told_the_hooks_need_it()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(
            """
            namespace Demo;

            public interface IClock { void Tick(); }

            public static class Sample
            {
                public static IClock Wrap(IClock clock) => CallProxy.Create<IClock>(clock);
            }
            """,
            LanguageVersion.CSharp13);

        // Act
        var runResult = GeneratorTestHarness.Run(compilation);

        // Assert
        GeneratorTestHarness.GetHintNames(runResult).ShouldBeEmpty();
        var diagnostic = GeneratorTestHarness.GetDiagnostics(runResult).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(CallProxyDiagnosticIds.LanguageVersion);
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe("CallProxy typed hooks are C# 14 extension members; this project compiles C# 13.0");
    }

    [Fact]
    public void The_same_problem_in_several_places_is_reported_once_per_place()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation("""
            namespace Demo;

            public class Clock { }

            public static class Sample
            {
                public static void Wrap(Clock clock)
                {
                    CallProxy.Create<Clock>(clock);
                    CallProxy.Create<Clock>(clock);
                }
            }
            """);

        // Act
        var diagnostics = GeneratorTestHarness.GetDiagnostics(GeneratorTestHarness.Run(compilation));

        // Assert
        diagnostics.ShouldHaveCount(2);
        diagnostics.Select(static diagnostic => diagnostic.Location.SourceSpan.Start).Distinct().ShouldHaveCount(2);
    }
}
