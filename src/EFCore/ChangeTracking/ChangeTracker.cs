// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.ChangeTracking;

/// <summary>
///     Provides access to change tracking information and operations for entity instances the context is tracking.
///     Instances of this class are typically obtained from <see cref="DbContext.ChangeTracker" /> and it is not designed
///     to be directly constructed in your application code.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
/// </remarks>
public class ChangeTracker : IResettableService
{
    private readonly IRuntimeModel _model;
    private QueryTrackingBehavior _queryTrackingBehavior;
    private readonly QueryTrackingBehavior _defaultQueryTrackingBehavior;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    [EntityFrameworkInternal]
    public ChangeTracker(
        DbContext context,
        IStateManager stateManager,
        IChangeDetector changeDetector,
        IModel model,
        IEntityEntryGraphIterator graphIterator)
    {
        Context = context;

        _defaultQueryTrackingBehavior
            = context
                .GetService<IDbContextOptions>()
                .Extensions
                .OfType<CoreOptionsExtension>()
                .FirstOrDefault()
                ?.QueryTrackingBehavior
            ?? QueryTrackingBehavior.TrackAll;

        _queryTrackingBehavior = _defaultQueryTrackingBehavior;

        StateManager = stateManager;
        ChangeDetector = changeDetector;
        _model = (IRuntimeModel)model;
        GraphIterator = graphIterator;
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="DetectChanges()" /> method is called
    ///     automatically by methods of <see cref="DbContext" /> and related classes.
    /// </summary>
    /// <remarks>
    ///     The default value is true. This ensures the context is aware of any changes to tracked entity instances
    ///     before performing operations such as <see cref="DbContext.SaveChanges()" /> or returning change tracking
    ///     information. If you disable automatic detect changes then you must ensure that
    ///     <see cref="DetectChanges()" /> is called when entity instances have been modified.
    ///     Failure to do so may result in some changes not being persisted during
    ///     <see cref="DbContext.SaveChanges()" /> or out-of-date change tracking information being returned.
    /// </remarks>
    public virtual bool AutoDetectChangesEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether navigation properties for tracked entities
    ///     will be loaded on first access.
    /// </summary>
    /// <remarks>
    ///     The default value is true. However, lazy loading will only occur for navigation properties
    ///     of entities that have also been configured in the model for lazy loading.
    /// </remarks>
    public virtual bool LazyLoadingEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the tracking behavior for LINQ queries run against the context. Disabling change tracking
    ///     is useful for read-only scenarios because it avoids the overhead of setting up change tracking for each
    ///     entity instance. You should not disable change tracking if you want to manipulate entity instances and
    ///     persist those changes to the database using <see cref="DbContext.SaveChanges()" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method sets the default behavior for the context, but you can override this behavior for individual
    ///         queries using the <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})" />
    ///         and <see cref="EntityFrameworkQueryableExtensions.AsTracking{TEntity}(IQueryable{TEntity})" /> methods.
    ///     </para>
    ///     <para>
    ///         The default value is <see cref="Microsoft.EntityFrameworkCore.QueryTrackingBehavior.TrackAll" />. This means
    ///         the change tracker will keep track of changes for all entities that are returned from a LINQ query.
    ///     </para>
    /// </remarks>
    public virtual QueryTrackingBehavior QueryTrackingBehavior
    {
        get => _queryTrackingBehavior;
        set => _queryTrackingBehavior = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating when a dependent/child entity will have its state
    ///     set to <see cref="EntityState.Deleted" /> once severed from a parent/principal entity
    ///     through either a navigation or foreign key property being set to null. The default
    ///     value is <see cref="CascadeTiming.Immediate" />.
    /// </summary>
    /// <remarks>
    ///     Dependent/child entities are only deleted automatically when the relationship
    ///     is configured with <see cref="DeleteBehavior.Cascade" />. This is set by default
    ///     for required relationships.
    /// </remarks>
    public virtual CascadeTiming DeleteOrphansTiming
    {
        get => StateManager.DeleteOrphansTiming;
        set => StateManager.DeleteOrphansTiming = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating when a dependent/child entity will have its state
    ///     set to <see cref="EntityState.Deleted" /> once its parent/principal entity has been marked
    ///     as <see cref="EntityState.Deleted" />. The default value is<see cref="CascadeTiming.Immediate" />.
    /// </summary>
    /// <remarks>
    ///     Dependent/child entities are only deleted automatically when the relationship
    ///     is configured with <see cref="DeleteBehavior.Cascade" />. This is set by default
    ///     for required relationships.
    /// </remarks>
    public virtual CascadeTiming CascadeDeleteTiming
    {
        get => StateManager.CascadeDeleteTiming;
        set => StateManager.CascadeDeleteTiming = value;
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context.
    ///     The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> Entries()
    {
        TryDetectChanges();

        return StateManager.Entries.Select(e => new EntityEntry(e));
    }

    /// <summary>
    ///     Gets an <see cref="EntityEntry" /> for all entities of a given type being tracked by the context.
    ///     The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of entities to get entries for.</typeparam>
    /// <returns>An entry for each entity of the given type that is being tracked.</returns>
    public virtual IEnumerable<EntityEntry<TEntity>> Entries<TEntity>()
        where TEntity : class
    {
        TryDetectChanges();

        return StateManager.Entries
            .Where(e => e.Entity is TEntity)
            .Select(e => new EntityEntry<TEntity>(e));
    }

    private void TryDetectChanges()
    {
        if (AutoDetectChangesEnabled)
        {
            DetectChanges();
        }
    }

    /// <summary>
    ///     Checks if any new, deleted, or changed entities are being tracked
    ///     such that these changes will be sent to the database if <see cref="DbContext.SaveChanges()" />
    ///     or <see cref="DbContext.SaveChangesAsync(CancellationToken)" /> is called.
    /// </summary>
    /// <remarks>
    ///     Note that this method calls <see cref="DetectChanges" /> unless
    ///     <see cref="AutoDetectChangesEnabled" /> has been set to <see langword="false" />.
    /// </remarks>
    /// <returns><see langword="true" /> if there are changes to save, otherwise <see langword="false" />.</returns>
    public virtual bool HasChanges()
    {
        TryDetectChanges();

        return StateManager.ChangedCount > 0;
    }

    /// <summary>
    ///     Gets the context this change tracker belongs to.
    /// </summary>
    public virtual DbContext Context { get; }

    /// <summary>
    ///     Scans the tracked entity instances to detect any changes made to the instance data. <see cref="DetectChanges()" />
    ///     is usually called automatically by the context when up-to-date information is required (before
    ///     <see cref="DbContext.SaveChanges()" /> and when returning change tracking information). You typically only need to
    ///     call this method if you have disabled <see cref="AutoDetectChangesEnabled" />.
    /// </summary>
    public virtual void DetectChanges()
    {
        if (!_model.SkipDetectChanges)
        {
            ChangeDetector.DetectChanges(StateManager);
        }
    }

    /// <summary>
    ///     Accepts all changes made to entities in the context. It will be assumed that the tracked entities
    ///     represent the current state of the database. This method is typically called by <see cref="DbContext.SaveChanges()" />
    ///     after changes have been successfully saved to the database.
    /// </summary>
    public virtual void AcceptAllChanges()
        => StateManager.AcceptAllChanges();

    /// <summary>
    ///     Begins tracking an entity and any entities that are reachable by traversing its navigation properties.
    ///     Traversal is recursive so the navigation properties of any discovered entities will also be scanned.
    ///     The specified <paramref name="callback" /> is called for each discovered entity and must set the
    ///     <see cref="EntityEntry.State" /> that each entity should be tracked in. If no state is set, the entity
    ///     remains untracked.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is designed for use in disconnected scenarios where entities are retrieved using one instance of
    ///         the context and then changes are saved using a different instance of the context. An example of this is a
    ///         web service where one service call retrieves entities from the database and another service call persists
    ///         any changes to the entities. Each service call uses a new instance of the context that is disposed when the
    ///         call is complete.
    ///     </para>
    ///     <para>
    ///         If an entity is discovered that is already tracked by the context, that entity is not processed (and its
    ///         navigation properties are not traversed).
    ///     </para>
    /// </remarks>
    /// <param name="rootEntity">The entity to begin traversal from.</param>
    /// <param name="callback">
    ///     An action to configure the change tracking information for each entity. For the entity to begin being tracked,
    ///     the <see cref="EntityEntry.State" /> must be set.
    /// </param>
    public virtual void TrackGraph(
        object rootEntity,
        Action<EntityEntryGraphNode> callback)
        => TrackGraph(
            rootEntity,
            callback,
            n =>
            {
                if (n.Entry.State != EntityState.Detached)
                {
                    return false;
                }

                n.NodeState!(n);

                return n.Entry.State != EntityState.Detached;
            });

    /// <summary>
    ///     Begins tracking an entity and any entities that are reachable by traversing its navigation properties.
    ///     Traversal is recursive so the navigation properties of any discovered entities will also be scanned.
    ///     The specified <paramref name="callback" /> is called for each discovered entity and must set the
    ///     <see cref="EntityEntry.State" /> that each entity should be tracked in. If no state is set, the entity
    ///     remains untracked.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is designed for use in disconnected scenarios where entities are retrieved using one instance of
    ///         the context and then changes are saved using a different instance of the context. An example of this is a
    ///         web service where one service call retrieves entities from the database and another service call persists
    ///         any changes to the entities. Each service call uses a new instance of the context that is disposed when the
    ///         call is complete.
    ///     </para>
    ///     <para>
    ///         Typically traversal of the graph should stop whenever an already tracked entity is encountered or when
    ///         an entity is reached that should not be tracked. For this typical behavior, use the
    ///         <see cref="TrackGraph" /> overload. This overload, on the other hand,
    ///         allows the callback to decide when traversal will end, but the onus is then on the caller to ensure that
    ///         traversal will not enter an infinite loop.
    ///     </para>
    /// </remarks>
    /// <param name="rootEntity">The entity to begin traversal from.</param>
    /// <param name="state">An arbitrary state object passed to the callback.</param>
    /// <param name="callback">
    ///     An delegate to configure the change tracking information for each entity. The second parameter to the
    ///     callback is the arbitrary state object passed above. Iteration of the graph will not continue down the graph
    ///     if the callback returns <see langword="false" />.
    /// </param>
    /// <typeparam name="TState">The type of the state object.</typeparam>
    public virtual void TrackGraph<TState>(
        object rootEntity,
        TState state,
        Func<EntityEntryGraphNode<TState>, bool> callback)
    {
        Check.NotNull(rootEntity, nameof(rootEntity));
        Check.NotNull(callback, nameof(callback));

        var rootEntry = StateManager.GetOrCreateEntry(rootEntity);

        try
        {
            rootEntry.StateManager.BeginAttachGraph();

            GraphIterator.TraverseGraph(
                new EntityEntryGraphNode<TState>(rootEntry, state, null, null),
                callback);

            rootEntry.StateManager.CompleteAttachGraph();
        }
        catch
        {
            rootEntry.StateManager.AbortAttachGraph();
            throw;
        }
    }

    private IStateManager StateManager { get; }

    private IChangeDetector ChangeDetector { get; }

    private IEntityEntryGraphIterator GraphIterator { get; }

    /// <summary>
    ///     An event fired when an entity is tracked by the context, either because it was returned
    ///     from a tracking query, or because it was attached or added to the context.
    /// </summary>
    public event EventHandler<EntityTrackedEventArgs> Tracked
    {
        add => StateManager.Tracked += value;
        remove => StateManager.Tracked -= value;
    }

    /// <summary>
    ///     An event fired when an entity that is tracked by the associated <see cref="DbContext" /> has moved
    ///     from one <see cref="EntityState" /> to another.
    /// </summary>
    /// <remarks>
    ///     Note that this event does not fire for entities when they are first tracked by the context.
    ///     Use the <see cref="Tracked" /> event to get notified when the context begins tracking an entity.
    /// </remarks>
    public event EventHandler<EntityStateChangedEventArgs> StateChanged
    {
        add => StateManager.StateChanged += value;
        remove => StateManager.StateChanged -= value;
    }

    /// <summary>
    ///     Forces immediate cascading deletion of child/dependent entities when they are either
    ///     severed from a required parent/principal entity, or the required parent/principal entity
    ///     is itself deleted. See <see cref="DeleteBehavior" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is usually used when <see cref="CascadeDeleteTiming" /> and/or
    ///         <see cref="DeleteOrphansTiming" /> have been set to <see cref="CascadeTiming.Never" />
    ///         to manually force the deletes to have at a time controlled by the application.
    ///     </para>
    ///     <para>
    ///         If <see cref="AutoDetectChangesEnabled" /> is <see langword="true" /> then this method
    ///         will call <see cref="DetectChanges" />.
    ///     </para>
    /// </remarks>
    public virtual void CascadeChanges()
    {
        if (AutoDetectChangesEnabled)
        {
            DetectChanges();
        }

        StateManager.CascadeChanges(force: true);
    }

    /// <inheritdoc />
    void IResettableService.ResetState()
    {
        _queryTrackingBehavior = _defaultQueryTrackingBehavior;
        AutoDetectChangesEnabled = true;
        LazyLoadingEnabled = true;
        CascadeDeleteTiming = CascadeTiming.Immediate;
        DeleteOrphansTiming = CascadeTiming.Immediate;
    }

    Task IResettableService.ResetStateAsync(CancellationToken cancellationToken)
    {
        ((IResettableService)this).ResetState();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops tracking all currently tracked entities.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="DbContext" /> is designed to have a short lifetime where a new instance is created for each unit-of-work.
    ///         This manner means all tracked entities are discarded when the context is disposed at the end of each unit-of-work.
    ///         However, clearing all tracked entities using this method may be useful in situations where creating a new context
    ///         instance is not practical.
    ///     </para>
    ///     <para>
    ///         This method should always be preferred over detaching every tracked entity.
    ///         Detaching entities is a slow process that may have side effects.
    ///         This method is much more efficient at clearing all tracked entities from the context.
    ///     </para>
    ///     <para>
    ///         Note that this method does not generate <see cref="StateChanged" /> events since entities are not individually detached.
    ///     </para>
    /// </remarks>
    public virtual void Clear()
        => StateManager.Clear();

    /// <summary>
    ///     Finds an <see cref="EntityEntry"/> for the entity with the given primary key value in the change tracker, if it is being
    ///     tracked. <see langword="null" /> is returned if no entity with the given key value is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="keyValue">The value of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry? FindEntry(Type entityType, object? keyValue)
    {
        var internalEntityEntry = CreateFinder(entityType, null).FindEntry(new[] { keyValue });

        return internalEntityEntry == null ? null : new EntityEntry(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry"/> for the entity with the given primary key values in the change tracker, if it is being
    ///     tracked. <see langword="null" /> is returned if no entity with the given key values is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry? FindEntry(Type entityType, IEnumerable<object?> keyValues)
    {
        Check.NotNull(keyValues, nameof(keyValues));

        var internalEntityEntry = CreateFinder(entityType, null).FindEntry(keyValues);

        return internalEntityEntry == null ? null : new EntityEntry(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry{TEntity}"/> for the entity with the given primary key value in the change tracker, if it is
    ///     being tracked. <see langword="null" /> is returned if no entity with the given key value is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <typeparam name="TKey">The type of the primary key property.</typeparam>
    /// <param name="keyValue">The value of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry<TEntity>? FindEntry<TEntity, TKey>(TKey keyValue)
        where TEntity : class
    {
        var internalEntityEntry = CreateFinder(typeof(TEntity), null).FindEntry(keyValue);

        return internalEntityEntry == null ? null : new EntityEntry<TEntity>(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry{TEntity}"/> for the entity with the given primary key values in the change tracker, if it is
    ///     being tracked. <see langword="null" /> is returned if no entity with the given key values is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry<TEntity>? FindEntry<TEntity>(IEnumerable<object?> keyValues)
        where TEntity : class
    {
        var internalEntityEntry = CreateFinder(typeof(TEntity), null).FindEntry(keyValues);

        return internalEntityEntry == null ? null : new EntityEntry<TEntity>(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry"/> for the entity with the given primary key value in the change tracker, if it is being
    ///     tracked. <see langword="null" /> is returned if no entity with the given key value is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="keyValue">The value of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry? FindEntry(string entityTypeName, object? keyValue)
    {
        Check.NotEmpty(entityTypeName, nameof(entityTypeName));

        var internalEntityEntry = CreateFinder(null, entityTypeName).FindEntry(new[] { keyValue });

        return internalEntityEntry == null ? null : new EntityEntry(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry"/> for the entity with the given primary key values in the change tracker, if it is being
    ///     tracked. <see langword="null" /> is returned if no entity with the given key values is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry? FindEntry(string entityTypeName, IEnumerable<object?> keyValues)
    {
        Check.NotEmpty(entityTypeName, nameof(entityTypeName));
        Check.NotNull(keyValues, nameof(keyValues));

        var internalEntityEntry = CreateFinder(null, entityTypeName).FindEntry(keyValues);

        return internalEntityEntry == null ? null : new EntityEntry(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry{TEntity}"/> for the entity with the given primary key value in the change tracker, if it is
    ///     being tracked. <see langword="null" /> is returned if no entity with the given key value is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <typeparam name="TKey">The type of the primary key property.</typeparam>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="keyValue">The value of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry<TEntity>? FindEntry<TEntity, TKey>(string entityTypeName, TKey keyValue)
        where TEntity : class
    {
        Check.NotEmpty(entityTypeName, nameof(entityTypeName));

        var internalEntityEntry = CreateFinder(typeof(TEntity), entityTypeName).FindEntry(new object?[] { keyValue });

        return internalEntityEntry == null ? null : new EntityEntry<TEntity>(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Finds an <see cref="EntityEntry{TEntity}"/> for the entity with the given primary key values in the change tracker, if it is
    ///     being tracked. <see langword="null" /> is returned if no entity with the given key values is being tracked.
    ///     This method never queries the database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-change-tracking">EF Core change tracking</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity to find.</typeparam>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <returns>An entry for the entity found, or <see langword="null" />.</returns>
    public virtual EntityEntry<TEntity>? FindEntry<TEntity>(string entityTypeName, IEnumerable<object?> keyValues)
        where TEntity : class
    {
        Check.NotEmpty(entityTypeName, nameof(entityTypeName));

        var internalEntityEntry = CreateFinder(typeof(TEntity), entityTypeName).FindEntry(keyValues);

        return internalEntityEntry == null ? null : new EntityEntry<TEntity>(TryDetectChanges(internalEntityEntry));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries(Type entityType, string propertyName, object? propertyValue)
    {
        TryDetectChanges();

        // Use value comparer
        return Entries().Where(e => e.Metadata.ClrType == entityType && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry{TEntity}" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <typeparam name="TEntity">The type of entities to get entries for.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry<TEntity>> GetEntries<TEntity>(string propertyName, object? propertyValue)
        where TEntity : class
    {
        TryDetectChanges();

        // Use value comparer
        return Entries<TEntity>().Where(e => Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries<TValue>(Type entityType, string propertyName, TValue propertyValue)
    {
        TryDetectChanges();

        // Use value comparer
        return Entries().Where(e => e.Metadata.ClrType == entityType && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given property
    ///     matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <typeparam name="TEntity">The type of entities to get entries for.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry<TEntity>> GetEntries<TEntity, TValue>(string propertyName, TValue propertyValue)
        where TEntity : class
    {
        TryDetectChanges();

        // Use value comparer
        return Entries<TEntity>().Where(e => Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the values of the given properties
    ///     matches the given values. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityType">The type of entity to find.</param>
    /// <param name="propertyNames">The name of the properties to match.</param>
    /// <param name="propertyValues">The values of the properties to match.</param>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries(
        Type entityType, IEnumerable<string> propertyNames, IEnumerable<object?> propertyValues)
    {
        TryDetectChanges();

        // Use value comparer
        return ImmutableArray<EntityEntry>.Empty;
        //return Entries().Where(e => e.Metadata.ClrType == entityType && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries(string entityTypeName, string propertyName, object? propertyValue)
    {
        TryDetectChanges();

        // Use value comparer
        return Entries().Where(e => e.Metadata.Name == entityTypeName && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry{TEntity}" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <typeparam name="TEntity">The type of entities to get entries for.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry<TEntity>> GetEntries<TEntity>(string entityTypeName, string propertyName, object? propertyValue)
        where TEntity : class
    {
        TryDetectChanges();

        // Use value comparer
        return Entries<TEntity>().Where(e => e.Metadata.Name == entityTypeName && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given
    ///     property matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries<TValue>(string entityTypeName, string propertyName, TValue propertyValue)
    {
        TryDetectChanges();

        // Use value comparer
        return Entries().Where(e => e.Metadata.Name == entityTypeName && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the the value of the given property
    ///     matches the given value. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="propertyName">The name of the property to match.</param>
    /// <param name="propertyValue">The value of the property to match.</param>
    /// <typeparam name="TEntity">The type of entities to get entries for.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry<TEntity>> GetEntries<TEntity, TValue>(
        string entityTypeName, string propertyName, TValue propertyValue)
        where TEntity : class
    {
        TryDetectChanges();

        // Use value comparer
        return Entries<TEntity>().Where(e => e.Metadata.Name == entityTypeName && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    /// <summary>
    ///     Returns an <see cref="EntityEntry" /> for each entity being tracked by the context where the values of the given properties
    ///     matches the given values. The entries provide access to change tracking information and operations for each entity.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is frequently used to get the entities with a given foreign key, primary key, or alternate key values.
    ///         Lookups using a key property like this is more efficient than lookups on other property values.
    ///     </para>
    ///     <para>
    ///         This method calls <see cref="DetectChanges" /> to ensure all entries returned reflect up-to-date state.
    ///         Use <see cref="AutoDetectChangesEnabled" /> to prevent DetectChanges from being called automatically.
    ///     </para>
    ///     <para>
    ///         Note that modification of entity state while iterating over the returned enumeration may result in
    ///         an <see cref="InvalidOperationException" /> indicating that the collection was modified while enumerating.
    ///         To avoid this, create a defensive copy using <see cref="Enumerable.ToList{TSource}" /> or similar before iterating.
    ///     </para>
    /// </remarks>
    /// <param name="entityTypeName">The full name of the entity type to find, which may be a shared-type entity type.</param>
    /// <param name="propertyNames">The name of the properties to match.</param>
    /// <param name="propertyValues">The values of the properties to match.</param>
    /// <returns>An entry for each entity being tracked.</returns>
    public virtual IEnumerable<EntityEntry> GetEntries(
        string entityTypeName, IEnumerable<string> propertyNames, IEnumerable<object?> propertyValues)
    {
        TryDetectChanges();

        // Use value comparer
        return ImmutableArray<EntityEntry>.Empty;
        //return Entries().Where(e => e.Metadata.ClrType == entityType && Equals(e.Property(propertyName).CurrentValue, propertyValue));
    }

    private IEntityFinder CreateFinder(Type? type, string? sharedTypeName)
    {
        var entityType = sharedTypeName != null
            ? _model.FindEntityType(sharedTypeName)
            : _model.FindEntityType(type!);

        if (entityType == null)
        {
            if (type != null)
            {
                if (_model.IsShared(type))
                {
                    throw new InvalidOperationException(CoreStrings.InvalidSetSharedType(type.ShortDisplayName()));
                }

                var findSameTypeName = _model.FindSameTypeNameWithDifferentNamespace(type);
                //if the same name exists in your entity types we will show you the full namespace of the type
                if (!string.IsNullOrEmpty(findSameTypeName))
                {
                    throw new InvalidOperationException(
                        CoreStrings.InvalidSetSameTypeWithDifferentNamespace(type.DisplayName(), findSameTypeName));
                }
            }

            throw new InvalidOperationException(CoreStrings.InvalidSetType(
                sharedTypeName ?? type!.ShortDisplayName()));
        }

        if (entityType.FindPrimaryKey() == null)
        {
            throw new InvalidOperationException(CoreStrings.InvalidSetKeylessOperation(
                sharedTypeName ?? type!.ShortDisplayName()));
        }

        return Context.GetDependencies().EntityFinderFactory.Create(entityType);
    }

    private InternalEntityEntry TryDetectChanges(InternalEntityEntry internalEntityEntry)
    {
        if (AutoDetectChangesEnabled && !_model.SkipDetectChanges)
        {
            ChangeDetector.DetectChanges(internalEntityEntry);
        }

        return internalEntityEntry;
    }

    /// <summary>
    ///     <para>
    ///         Expand this property in the debugger for a human-readable view of the entities being tracked.
    ///     </para>
    ///     <para>
    ///         Warning: Do not rely on the format of the debug strings.
    ///         They are designed for debugging only and may change arbitrarily between releases.
    ///     </para>
    /// </summary>
    public virtual DebugView DebugView
        => new(
            () => this.ToDebugString(ChangeTrackerDebugStringOptions.ShortDefault),
            () => this.ToDebugString());

    #region Hidden System.Object members

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string? ToString()
        => base.ToString();

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true" /> if the specified object is equal to the current object; otherwise, <see langword="false" />.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj)
        => base.Equals(obj);

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
        => base.GetHashCode();

    #endregion
}
