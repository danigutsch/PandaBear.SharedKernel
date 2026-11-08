namespace PandaBear.Results.Analyzers.Tests;

/// <summary>Builds the source snippets used by the Results analyzer tests.</summary>
internal static class ResultSources
{
    /// <summary>Wraps a configure body in a class that has an endpoint builder, a group builder and Result-returning helper methods.</summary>
    public static string Endpoints(string body) => $$"""
        namespace Demo;

        public sealed class Endpoints
        {
            public void Configure(IEndpointRouteBuilder app, RouteGroupBuilder group)
            {
                {{body}}
            }

            private static Result Run() => Result.Ok();

            private static Result<int> Load() => Result.Ok(1);

            private static Task<Result> RunTask() => Task.FromResult(Result.Ok());

            private static ValueTask<Result<int>> LoadValueTask() => new(Result.Ok(1));

            private static IResult Plain() => Results.Ok();
        }
        """;

    /// <summary>Wraps statements in an async method of a class that has Result-returning helper methods.</summary>
    public static string Statements(string body) => $$"""
        namespace Demo;

        public sealed class Service
        {
            public async Task<Result> Execute()
            {
                {{body}}
                return Result.Ok();
            }

            private static Result Run() => Result.Ok();

            private static Result<int> Load() => Result.Ok(1);

            private static Task<Result> RunTask() => Task.FromResult(Result.Ok());

            private static ValueTask<Result<int>> LoadValueTask() => new(Result.Ok(1));

            private static void Use(Result result) => _ = result;

            private static int Count() => 1;
        }
        """;
}
