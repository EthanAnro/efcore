// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Migrations.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class Migrator : IMigrator
{
    private readonly IMigrationsAssembly _migrationsAssembly;
    private readonly IHistoryRepository _historyRepository;
    private readonly IRelationalDatabaseCreator _databaseCreator;
    private readonly IMigrationsSqlGenerator _migrationsSqlGenerator;
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
    private readonly IMigrationCommandExecutor _migrationCommandExecutor;
    private readonly IRelationalConnection _connection;
    private readonly ISqlGenerationHelper _sqlGenerationHelper;
    private readonly ICurrentDbContext _currentContext;
    private readonly IModelRuntimeInitializer _modelRuntimeInitializer;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Migrations> _logger;
    private readonly IRelationalCommandDiagnosticsLogger _commandLogger;
    private readonly string _activeProvider;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public Migrator(
        IMigrationsAssembly migrationsAssembly,
        IHistoryRepository historyRepository,
        IDatabaseCreator databaseCreator,
        IMigrationsSqlGenerator migrationsSqlGenerator,
        IRawSqlCommandBuilder rawSqlCommandBuilder,
        IMigrationCommandExecutor migrationCommandExecutor,
        IRelationalConnection connection,
        ISqlGenerationHelper sqlGenerationHelper,
        ICurrentDbContext currentContext,
        IModelRuntimeInitializer modelRuntimeInitializer,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger,
        IRelationalCommandDiagnosticsLogger commandLogger,
        IDatabaseProvider databaseProvider)
    {
        _migrationsAssembly = migrationsAssembly;
        _historyRepository = historyRepository;
        _databaseCreator = (IRelationalDatabaseCreator)databaseCreator;
        _migrationsSqlGenerator = migrationsSqlGenerator;
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
        _migrationCommandExecutor = migrationCommandExecutor;
        _connection = connection;
        _sqlGenerationHelper = sqlGenerationHelper;
        _currentContext = currentContext;
        _modelRuntimeInitializer = modelRuntimeInitializer;
        _logger = logger;
        _commandLogger = commandLogger;
        _activeProvider = databaseProvider.Name;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual TimeSpan LockTimeout { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual void Migrate(string? targetMigration = null)
    {
        _logger.MigrateUsingConnection(this, _connection);

        if (!_databaseCreator.Exists())
        {
            _databaseCreator.Create();
        }

        try
        {
            _connection.Open();

            using var _ = _historyRepository.GetDatabaseLock(LockTimeout);

            if (!_historyRepository.Exists())
            {
                _historyRepository.Create();
            }

            var commandLists = GetMigrationCommandLists(_historyRepository.GetAppliedMigrations(), targetMigration);

            foreach (var commandList in commandLists)
            {
                _migrationCommandExecutor.ExecuteNonQuery(commandList(), _connection);
            }
        }
        finally
        {
            _connection.Close();
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual async Task MigrateAsync(
        string? targetMigration = null,
        CancellationToken cancellationToken = default)
    {
        _logger.MigrateUsingConnection(this, _connection);

        if (!await _databaseCreator.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await _databaseCreator.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var dbLock = await _historyRepository.GetDatabaseLockAsync(LockTimeout, cancellationToken).ConfigureAwait(false);
            await using var _ = dbLock.ConfigureAwait(false);

            if (!await _historyRepository.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await _historyRepository.CreateAsync(cancellationToken).ConfigureAwait(false);
            }

            var commandLists = GetMigrationCommandLists(
                await _historyRepository.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false),
                targetMigration);

            foreach (var commandList in commandLists)
            {
                await _migrationCommandExecutor.ExecuteNonQueryAsync(commandList(), _connection, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _connection.Close();
        }
    }

    private IEnumerable<Func<IReadOnlyList<MigrationCommand>>> GetMigrationCommandLists(
        IReadOnlyList<HistoryRow> appliedMigrationEntries,
        string? targetMigration = null)
    {
        PopulateMigrations(
            appliedMigrationEntries.Select(t => t.MigrationId),
            targetMigration,
            out var migrationsToApply,
            out var migrationsToRevert,
            out var actualTargetMigration);

        for (var i = 0; i < migrationsToRevert.Count; i++)
        {
            var migration = migrationsToRevert[i];

            var index = i;
            yield return () =>
            {
                _logger.MigrationReverting(this, migration);

                return GenerateDownSql(
                    migration,
                    index != migrationsToRevert.Count - 1
                        ? migrationsToRevert[index + 1]
                        : actualTargetMigration);
            };
        }

        foreach (var migration in migrationsToApply)
        {
            yield return () =>
            {
                _logger.MigrationApplying(this, migration);

                return GenerateUpSql(migration);
            };
        }

        if (migrationsToRevert.Count + migrationsToApply.Count == 0)
        {
            _logger.MigrationsNotApplied(this);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual void PopulateMigrations(
        IEnumerable<string> appliedMigrationEntries,
        string? targetMigration,
        out IReadOnlyList<Migration> migrationsToApply,
        out IReadOnlyList<Migration> migrationsToRevert,
        out Migration? actualTargetMigration)
    {
        var appliedMigrations = new Dictionary<string, TypeInfo>();
        var unappliedMigrations = new Dictionary<string, TypeInfo>();
        var appliedMigrationEntrySet = new HashSet<string>(appliedMigrationEntries, StringComparer.OrdinalIgnoreCase);
        if (_migrationsAssembly.Migrations.Count == 0)
        {
            _logger.MigrationsNotFound(this, _migrationsAssembly);
        }

        foreach (var (key, typeInfo) in _migrationsAssembly.Migrations)
        {
            if (appliedMigrationEntrySet.Contains(key))
            {
                appliedMigrations.Add(key, typeInfo);
            }
            else
            {
                unappliedMigrations.Add(key, typeInfo);
            }
        }

        if (string.IsNullOrEmpty(targetMigration))
        {
            migrationsToApply = unappliedMigrations
                .OrderBy(m => m.Key)
                .Select(p => _migrationsAssembly.CreateMigration(p.Value, _activeProvider))
                .ToList();
            migrationsToRevert = [];
            actualTargetMigration = null;
        }
        else if (targetMigration == Migration.InitialDatabase)
        {
            migrationsToApply = [];
            migrationsToRevert = appliedMigrations
                .OrderByDescending(m => m.Key)
                .Select(p => _migrationsAssembly.CreateMigration(p.Value, _activeProvider))
                .ToList();
            actualTargetMigration = null;
        }
        else
        {
            targetMigration = _migrationsAssembly.GetMigrationId(targetMigration);
            migrationsToApply = unappliedMigrations
                .Where(m => string.Compare(m.Key, targetMigration, StringComparison.OrdinalIgnoreCase) <= 0)
                .OrderBy(m => m.Key)
                .Select(p => _migrationsAssembly.CreateMigration(p.Value, _activeProvider))
                .ToList();
            migrationsToRevert = appliedMigrations
                .Where(m => string.Compare(m.Key, targetMigration, StringComparison.OrdinalIgnoreCase) > 0)
                .OrderByDescending(m => m.Key)
                .Select(p => _migrationsAssembly.CreateMigration(p.Value, _activeProvider))
                .ToList();
            actualTargetMigration = appliedMigrations
                .Where(m => string.Compare(m.Key, targetMigration, StringComparison.OrdinalIgnoreCase) == 0)
                .Select(p => _migrationsAssembly.CreateMigration(p.Value, _activeProvider))
                .SingleOrDefault();
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual string GenerateScript(
        string? fromMigration = null,
        string? toMigration = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        options |= MigrationsSqlGenerationOptions.Script;

        var idempotent = options.HasFlag(MigrationsSqlGenerationOptions.Idempotent);
        var noTransactions = options.HasFlag(MigrationsSqlGenerationOptions.NoTransactions);

        IEnumerable<string> appliedMigrations;
        if (string.IsNullOrEmpty(fromMigration)
            || fromMigration == Migration.InitialDatabase)
        {
            appliedMigrations = Enumerable.Empty<string>();
        }
        else
        {
            var fromMigrationId = _migrationsAssembly.GetMigrationId(fromMigration);
            appliedMigrations = _migrationsAssembly.Migrations
                .Where(t => string.Compare(t.Key, fromMigrationId, StringComparison.OrdinalIgnoreCase) <= 0)
                .Select(t => t.Key);
        }

        PopulateMigrations(
            appliedMigrations,
            toMigration,
            out var migrationsToApply,
            out var migrationsToRevert,
            out var actualTargetMigration);

        var builder = new IndentedStringBuilder();

        if (fromMigration == Migration.InitialDatabase
            || string.IsNullOrEmpty(fromMigration))
        {
            builder
                .Append(_historyRepository.GetCreateIfNotExistsScript())
                .Append(_sqlGenerationHelper.BatchTerminator);
        }

        var idempotencyEnd = idempotent
            ? _historyRepository.GetEndIfScript()
            : null;
        for (var i = 0; i < migrationsToRevert.Count; i++)
        {
            var migration = migrationsToRevert[i];
            var previousMigration = i != migrationsToRevert.Count - 1
                ? migrationsToRevert[i + 1]
                : actualTargetMigration;

            _logger.MigrationGeneratingDownScript(this, migration, fromMigration, toMigration, idempotent);

            var idempotencyCondition = idempotent
                ? _historyRepository.GetBeginIfExistsScript(migration.GetId())
                : null;

            GenerateSqlScript(GenerateDownSql(migration, previousMigration, options), builder, _sqlGenerationHelper, noTransactions, idempotencyCondition, idempotencyEnd);
        }

        foreach (var migration in migrationsToApply)
        {
            _logger.MigrationGeneratingUpScript(this, migration, fromMigration, toMigration, idempotent);

            var idempotencyCondition = idempotent
                ? _historyRepository.GetBeginIfNotExistsScript(migration.GetId())
                : null;

            GenerateSqlScript(GenerateUpSql(migration, options), builder, _sqlGenerationHelper, noTransactions, idempotencyCondition, idempotencyEnd);
        }

        return builder.ToString();
    }

    private static void GenerateSqlScript(
        IEnumerable<MigrationCommand> commands,
        IndentedStringBuilder builder,
        ISqlGenerationHelper sqlGenerationHelper,
        bool noTransactions = false,
        string? idempotencyCondition = null,
        string? idempotencyEnd = null)
    {
        var transactionStarted = false;
        foreach (var command in commands)
        {
            if (!noTransactions)
            {
                if (!transactionStarted && !command.TransactionSuppressed)
                {
                    builder
                        .AppendLine(sqlGenerationHelper.StartTransactionStatement)
                        .Append(sqlGenerationHelper.BatchTerminator);
                    transactionStarted = true;
                }

                if (transactionStarted && command.TransactionSuppressed)
                {
                    builder
                        .AppendLine(sqlGenerationHelper.CommitTransactionStatement)
                        .Append(sqlGenerationHelper.BatchTerminator);
                    transactionStarted = false;
                }
            }

            if (idempotencyCondition != null
                && idempotencyEnd != null)
            {
                builder.AppendLine(idempotencyCondition);
                using (builder.Indent())
                {
                    builder.AppendLines(command.CommandText);
                }

                builder.Append(idempotencyEnd);
            }
            else
            {
                builder.Append(command.CommandText);
            }

            builder.Append(sqlGenerationHelper.BatchTerminator);
        }

        if (!noTransactions && transactionStarted)
        {
            builder
                .AppendLine(sqlGenerationHelper.CommitTransactionStatement)
                .Append(sqlGenerationHelper.BatchTerminator);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual IReadOnlyList<MigrationCommand> GenerateUpSql(
        Migration migration,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var insertCommand = _rawSqlCommandBuilder.Build(
            _historyRepository.GetInsertScript(new HistoryRow(migration.GetId(), ProductInfo.GetVersion())));

        return _migrationsSqlGenerator
            .Generate(migration.UpOperations, FinalizeModel(migration.TargetModel), options)
            .Concat([new MigrationCommand(insertCommand, _currentContext.Context, _commandLogger)])
            .ToList();
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual IReadOnlyList<MigrationCommand> GenerateDownSql(
        Migration migration,
        Migration? previousMigration,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var deleteCommand = _rawSqlCommandBuilder.Build(
            _historyRepository.GetDeleteScript(migration.GetId()));

        return _migrationsSqlGenerator
            .Generate(
                migration.DownOperations, previousMigration == null ? null : FinalizeModel(previousMigration.TargetModel), options)
            .Concat([new MigrationCommand(deleteCommand, _currentContext.Context, _commandLogger)])
            .ToList();
    }

    private IModel FinalizeModel(IModel model)
        => _modelRuntimeInitializer.Initialize(model, designTime: true, validationLogger: null);
}
