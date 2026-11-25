using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using PandaBear.AuditTrail;
using PandaBear.DocumentWorkflow;
using PandaBear.DocumentWorkflow.EntityFrameworkCore;
using PandaBear.EntityFrameworkCore.Npgsql;
using PandaBear.Idempotency.EntityFrameworkCore;
using PandaBear.Mediator;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Provides fresh units of work for the neutral document repository tests.</summary>
internal sealed class PostgreSqlDocumentWorkflowScenario : IAsyncDisposable
{
    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ServiceProvider provider;
    private readonly DocumentWorkflowSaveGate gate;

    private PostgreSqlDocumentWorkflowScenario(PostgreSqlTestDatabaseLease database, NpgsqlDataSource dataSource, ServiceProvider provider, DocumentWorkflowSaveGate gate)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.provider = provider;
        this.gate = gate;
    }

    /// <summary>Creates an isolated database and maps the document repository.</summary>
    public static async ValueTask<PostgreSqlDocumentWorkflowScenario> Create(
        PostgreSqlFixture fixture,
        CancellationToken ct,
        DocumentFinalizer<string>? finalizer = null,
        DateTimeOffset? utcNow = null)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;
        try
        {
            dataSource = database.CreateDataSource(nameof(PostgreSqlDocumentWorkflowScenario));
            var services = new ServiceCollection();
            services.AddNpgsqlUniqueViolationParser();
            services.AddDocumentWorkflowStorage<DocumentWorkflowTestContext>();
            services.AddIdempotencyStore<DocumentWorkflowTestContext>();
            if (finalizer is not null)
            {
                services.AddSingleton(finalizer);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(utcNow ?? DateTimeOffset.UnixEpoch));
                services.AddSingleton<ReleaseDocumentAuditFailure>();
                services.AddScoped<IAuditTrailSink<DocumentFinalizationAuditEntry>, ReleaseDocumentAuditSink>();
                services.AddDocumentFinalizationCommand<DocumentWorkflowTestContext, string>();
                services.AddMediator();
            }

            var gate = new DocumentWorkflowSaveGate();
            services.AddDbContext<DocumentWorkflowTestContext>(options => options.UseNpgsql(dataSource).AddInterceptors(gate));
            provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DocumentWorkflowTestContext>();
            await context.Database.EnsureCreatedAsync(ct);
            return new PostgreSqlDocumentWorkflowScenario(database, dataSource, provider, gate);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, provider, dataSource, database);
            throw;
        }
    }

    /// <summary>Invokes an operation with a fresh document store.</summary>
    public async ValueTask<Result<PersistedDocumentRevision>> Run(
        Func<DocumentWorkflowStore<DocumentWorkflowTestContext>, ValueTask<Result<PersistedDocumentRevision>>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<DocumentWorkflowStore<DocumentWorkflowTestContext>>());
    }

    /// <summary>Dispatches the command through the generated mediator and transaction behavior.</summary>
    public async ValueTask<Result<DocumentFinalizationReceipt>> Finalize(DocumentFinalizationRequest request, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new FinalizeDocumentRevisionCommand(request), ct);
    }

    /// <summary>Reads committed finalization evidence from a fresh scope.</summary>
    public async ValueTask<DocumentFinalizationAuditEntry[]> ReadAudit(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DocumentWorkflowTestContext>();
        return await context.Set<DocumentFinalizationAuditEntry>().AsNoTracking().ToArrayAsync(ct);
    }

    /// <summary>Injects an exception after the next audit save.</summary>
    public void FailNextAudit() => provider.GetRequiredService<ReleaseDocumentAuditFailure>().Arm();

    /// <summary>Holds the first save while a second writer commits from the same observed version.</summary>
    public async ValueTask<(Result<PersistedDocumentRevision> Lost, Result<PersistedDocumentRevision> Won)> Race(
        DocumentRevision first, DocumentRevision second, long version, CancellationToken ct)
    {
        gate.Arm();
        var waiting = Run(store => store.Save(first, version, ct)).AsTask();
        Result<PersistedDocumentRevision> winner;
        try
        {
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10), ct);
            winner = await Run(store => store.Save(second, version, ct));
        }
        finally
        {
            gate.Release();
        }

        return (await waiting, winner);
    }

    /// <summary>Creates a duplicate successor to make replacement hit the tagged uniqueness rule.</summary>
    public async Task BlockReplacement(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO document_workflow.revisions ("LineageId", "Number", "Version", "PayloadVersion", "Payload", "ArtifactReference")
            SELECT "LineageId", 2, "Version", "PayloadVersion", "Payload", "ArtifactReference"
            FROM document_workflow.revisions WHERE "Number" = 1;
            """);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Sets unrecognized or malformed payload data on the test revisions.</summary>
    public async Task CorruptPayload(bool futureVersion, CancellationToken ct)
    {
        var sql = futureVersion
            ? """UPDATE document_workflow.revisions SET "PayloadVersion" = 999;"""
            : """UPDATE document_workflow.revisions SET "Payload" = 'invalid-json';""";
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);
}
