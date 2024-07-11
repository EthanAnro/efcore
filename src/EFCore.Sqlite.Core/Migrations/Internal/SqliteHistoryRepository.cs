// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;

namespace Microsoft.EntityFrameworkCore.Sqlite.Migrations.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class SqliteHistoryRepository : HistoryRepository
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public SqliteHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override string ExistsSql => CreateExistsSql(TableName);

    /// <summary>
    ///     The name of the table that will serve as a database-wide lock for migrations.
    /// </summary>
    protected virtual string LockTableName { get; } = "__EFMigrationsLock";

    private string CreateExistsSql(string tableName)
    {
        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

        return $"""
SELECT COUNT(*) FROM "sqlite_master" WHERE "name" = {stringTypeMapping.GenerateSqlLiteral(tableName)} AND "type" = 'table';
""";
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override bool InterpretExistsResult(object? value)
        => (long)value! != 0L;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override string GetCreateIfNotExistsScript()
    {
        var script = GetCreateScript();
        return script.Insert(script.IndexOf("CREATE TABLE", StringComparison.Ordinal) + 12, " IF NOT EXISTS");
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override string GetBeginIfNotExistsScript(string migrationId)
        => throw new NotSupportedException(SqliteStrings.MigrationScriptGenerationNotSupported);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override string GetBeginIfExistsScript(string migrationId)
        => throw new NotSupportedException(SqliteStrings.MigrationScriptGenerationNotSupported);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override string GetEndIfScript()
        => throw new NotSupportedException(SqliteStrings.MigrationScriptGenerationNotSupported);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override IDisposable GetDatabaseLock(TimeSpan timeout)
    {
        if (!InterpretExistsResult(Dependencies.RawSqlCommandBuilder.Build(CreateExistsSql(LockTableName))
            .ExecuteScalar(CreateRelationalCommandParameters())))
        {
            CreateLockTableCommand().ExecuteNonQuery(CreateRelationalCommandParameters());
        }

        var retryDelay = _retryDelay;
        var startTime = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            var dbLock = CreateMigrationDatabaseLock();
            var insertCount = CreateInsertLockCommand(DateTimeOffset.UtcNow)
                .ExecuteScalar(CreateRelationalCommandParameters());
            if ((long)insertCount! == 1)
            {
                return dbLock;
            }

            using var reader = CreateGetLockCommand().ExecuteReader(CreateRelationalCommandParameters());
            if (reader.Read())
            {
                var timestamp = reader.DbDataReader.GetFieldValue<DateTimeOffset>(1);
                if (DateTimeOffset.UtcNow - timestamp > timeout)
                {
                    var id = reader.DbDataReader.GetFieldValue<int>(0);
                    CreateDeleteLockCommand(id).ExecuteNonQuery(CreateRelationalCommandParameters());
                }
            }

            Thread.Sleep(retryDelay);
            if (retryDelay < TimeSpan.FromMinutes(1))
            {
                retryDelay = retryDelay.Add(retryDelay);
            }
        }

        throw new TimeoutException();
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override async Task<IAsyncDisposable> GetDatabaseLockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!InterpretExistsResult(await Dependencies.RawSqlCommandBuilder.Build(CreateExistsSql(LockTableName))
            .ExecuteScalarAsync(CreateRelationalCommandParameters(), cancellationToken).ConfigureAwait(false)))
        {
            await CreateLockTableCommand().ExecuteNonQueryAsync(CreateRelationalCommandParameters(), cancellationToken).ConfigureAwait(false);
        }

        var retryDelay = _retryDelay;
        var startTime = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            var dbLock = CreateMigrationDatabaseLock();
            var insertCount = await CreateInsertLockCommand(DateTimeOffset.UtcNow)
                .ExecuteScalarAsync(CreateRelationalCommandParameters(), cancellationToken)
                .ConfigureAwait(false);
            if ((long)insertCount! == 1)
            {
                return dbLock;
            }

            using var reader = await CreateGetLockCommand().ExecuteReaderAsync(CreateRelationalCommandParameters(), cancellationToken)
                .ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var timestamp = await reader.DbDataReader.GetFieldValueAsync<DateTimeOffset>(1).ConfigureAwait(false);
                if (DateTimeOffset.UtcNow - timestamp > timeout)
                {
                    var id = await reader.DbDataReader.GetFieldValueAsync<int>(0).ConfigureAwait(false);
                    await CreateDeleteLockCommand(id).ExecuteNonQueryAsync(CreateRelationalCommandParameters(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(true);
            if (retryDelay < TimeSpan.FromMinutes(1))
            {
                retryDelay = retryDelay.Add(retryDelay);
            }
        }

        throw new TimeoutException();
    }

    private IRelationalCommand CreateLockTableCommand()
        => Dependencies.RawSqlCommandBuilder.Build($"""
CREATE TABLE IF NOT EXISTS "{LockTableName}" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_{LockTableName}" PRIMARY KEY,
    "Timestamp" TEXT NOT NULL
);
""");

    private IRelationalCommand CreateInsertLockCommand(DateTimeOffset timestamp)
    {
        var timestampLiteral = Dependencies.TypeMappingSource.GetMapping(typeof(DateTimeOffset)).GenerateSqlLiteral(timestamp);

        return Dependencies.RawSqlCommandBuilder.Build($"""
INSERT OR IGNORE INTO "{LockTableName}"("Id", "Timestamp") VALUES(1, {timestampLiteral});
SELECT changes();
""");
    }

    private IRelationalCommand CreateGetLockCommand()
        => Dependencies.RawSqlCommandBuilder.Build($"""
SELECT "Id", "Timestamp" FROM "{LockTableName}" LIMIT 1;
""");

    private IRelationalCommand CreateDeleteLockCommand(int? id = null)
    {
        var sql = $"""
DELETE FROM "{LockTableName}"
""";
        if (id != null)
        {
            sql += $""" WHERE "Id" = {id}""";
        }
        sql += ";";
        return Dependencies.RawSqlCommandBuilder.Build(sql);
    }

    private SqliteMigrationDatabaseLock CreateMigrationDatabaseLock()
        => new(CreateDeleteLockCommand(), CreateRelationalCommandParameters());

    private RelationalCommandParameterObject CreateRelationalCommandParameters()
        => new(
            Dependencies.Connection,
            null,
            null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger, CommandSource.Migrations);
}
