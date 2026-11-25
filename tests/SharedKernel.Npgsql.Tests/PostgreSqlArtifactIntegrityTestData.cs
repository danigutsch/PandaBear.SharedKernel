using System.Globalization;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Creates synthetic artifacts with independent schemas and SQL identifiers that require quoting.</summary>
internal static class PostgreSqlArtifactIntegrityTestData
{
    /// <summary>Asymmetric identifiers exercise quoting and schema qualification independently.</summary>
    internal static readonly PostgreSqlArtifactIntegrityMapping Mapping = new(
        MetadataSchema: "Report \"case\"",
        MetadataTable: "Stored metadata",
        IdColumn: "Opaque Id",
        EligibilityColumn: "Observed At",
        LengthColumn: "Recorded Length",
        Sha256Column: "Digest",
        BodySchema: "Byte Store",
        BodyTable: "Blob\"Rows",
        BodyIdColumn: "Join Id",
        BodyContentColumn: "Bytes");

    /// <summary>Creates the independent tables and a case-insensitive collation for the stored digest.</summary>
    internal const string CreateTables = """"
        CREATE SCHEMA "Report ""case""";
        CREATE SCHEMA "Byte Store";
        CREATE COLLATION "Report ""case"""."Fold Digest" (provider = icu, locale = 'und-u-ks-level2', deterministic = false);
        CREATE TABLE "Report ""case"""."Stored metadata" (
            "Opaque Id" uuid PRIMARY KEY,
            "Observed At" timestamptz,
            "Recorded Length" integer,
            "Digest" text COLLATE "Report ""case"""."Fold Digest"
        );
        CREATE TABLE "Byte Store"."Blob""Rows" ("Join Id" uuid PRIMARY KEY, "Bytes" bytea);
        """" + "\n";

    /// <summary>Creates every outcome, including rows with multiple faults to verify precedence.</summary>
    internal const string PopulateIntegrityCases = """"
        INSERT INTO "Report ""case"""."Stored metadata"
        SELECT ('00000000-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
            CASE WHEN id <> 9 THEN now() END,
            CASE id WHEN 2 THEN NULL WHEN 3 THEN NULL WHEN 4 THEN 0 WHEN 5 THEN 4 WHEN 11 THEN -1 ELSE 3 END,
            CASE
                WHEN id IN (2, 10) THEN NULL
                WHEN id = 7 THEN upper(encode(sha256('abc'::bytea), 'hex'))
                ELSE encode(sha256('abc'::bytea), 'hex')
            END
        FROM generate_series(1, 11) AS ids(id);

        INSERT INTO "Byte Store"."Blob""Rows"
        SELECT ('00000000-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
            CASE WHEN id = 4 THEN ''::bytea WHEN id = 8 THEN NULL WHEN id IN (5, 6) THEN 'abd'::bytea ELSE 'abc'::bytea END
        FROM generate_series(1, 11) AS ids(id)
        WHERE id <> 2;
        """" + "\n";

    /// <summary>Creates 101 eligible rows interleaved with rows that lack the inclusion signal.</summary>
    internal const string PopulatePage = """"
        INSERT INTO "Report ""case"""."Stored metadata"
        SELECT ('00000000-0000-0000-0000-' || lpad(id::text, 12, '0'))::uuid,
            CASE WHEN id % 2 = 0 THEN now() END, NULL, NULL
        FROM generate_series(0, 200) AS ids(id)
        ORDER BY id DESC;
        """";

    /// <summary>Returns the intact first row only if the reader sets the transaction controls.</summary>
    internal const string CreateReadOnlyView = """"
        CREATE VIEW "Report ""case"""."Transaction settings" AS
        SELECT "Opaque Id", "Observed At",
            CASE WHEN current_setting('transaction_read_only') = 'on'
                AND current_setting('statement_timeout') <> '0' THEN "Recorded Length" ELSE 0 END AS "Recorded Length",
            "Digest"
        FROM "Report ""case"""."Stored metadata"
        WHERE "Opaque Id" = '00000000-0000-0000-0000-000000000001';
        """";

    /// <summary>Formats a deterministic UUID matching the SQL fixtures.</summary>
    internal static Guid Revision(int id) =>
        Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-0000-0000-{id:000000000000}"));
}
