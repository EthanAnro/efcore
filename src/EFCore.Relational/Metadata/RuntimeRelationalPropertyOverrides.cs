// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
///     Represents property facet overrides for a particular table-like store object.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more information and examples.
/// </remarks>
public class RuntimeRelationalPropertyOverrides : AnnotatableBase, IRelationalPropertyOverrides
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RuntimeRelationalPropertyOverrides" /> class.
    /// </summary>
    /// <param name="property">The property for which the overrides are applied.</param>
    /// <param name="columnNameOverridden">Whether the column name is overridden.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="storeObject">The store object for which the configuration is applied.</param>
    public RuntimeRelationalPropertyOverrides(
        RuntimeProperty property,
        bool columnNameOverridden,
        string? columnName,
        in StoreObjectIdentifier storeObject)
    {
        Property = property;
        StoreObject = storeObject;
        if (columnNameOverridden)
        {
            SetAnnotation(RelationalAnnotationNames.ColumnName, columnName);
        }
    }

    /// <summary>
    ///     Gets the property for which the overrides are applied.
    /// </summary>
    public virtual RuntimeProperty Property { get; }

    /// <summary>
    ///     Gets store object for which the configuration is applied.
    /// </summary>
    public virtual StoreObjectIdentifier StoreObject { get; }

    /// <inheritdoc />
    IProperty IRelationalPropertyOverrides.Property
    {
        [DebuggerStepThrough]
        get => Property;
    }

    /// <inheritdoc />
    string? IRelationalPropertyOverrides.ColumnName
    {
        [DebuggerStepThrough]
        get => (string?)this[RelationalAnnotationNames.ColumnName];
    }

    /// <inheritdoc />
    bool IRelationalPropertyOverrides.ColumnNameOverridden
    {
        [DebuggerStepThrough]
        get => FindAnnotation(RelationalAnnotationNames.ColumnName) != null;
    }
}
