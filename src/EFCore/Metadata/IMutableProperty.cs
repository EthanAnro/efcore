// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Microsoft.EntityFrameworkCore.Metadata
{
    /// <summary>
    ///     Represents a scalar property of an entity type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This interface is used during model creation and allows the metadata to be modified.
    ///         Once the model is built, <see cref="IProperty" /> represents a read-only view of the same metadata.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more information and
    ///         examples.
    ///     </para>
    /// </remarks>
    public interface IMutableProperty : IReadOnlyProperty, IMutablePropertyBase
    {
        /// <summary>
        ///     Gets the type that this property belongs to.
        /// </summary>
        new IMutableEntityType DeclaringEntityType { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether this property can contain <see langword="null" />.
        /// </summary>
        new bool IsNullable { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating when a value for this property will be generated by the database. Even when the
        ///     property is set to be generated by the database, EF may still attempt to save a specific value (rather than
        ///     having one generated by the database) when the entity is added and a value is assigned, or the property is
        ///     marked as modified for an existing entity. See <see cref="IReadOnlyProperty.GetBeforeSaveBehavior" />
        ///     and <see cref="IReadOnlyProperty.GetAfterSaveBehavior" /> for more information and examples.
        /// </summary>
        new ValueGenerated ValueGenerated { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this property is used as a concurrency token. When a property is configured
        ///     as a concurrency token the value in the database will be checked when an instance of this entity type
        ///     is updated or deleted during <see cref="DbContext.SaveChanges()" /> to ensure it has not changed since
        ///     the instance was retrieved from the database. If it has changed, an exception will be thrown and the
        ///     changes will not be applied to the database.
        /// </summary>
        new bool IsConcurrencyToken { get; set; }

        /// <summary>
        ///     Finds the first principal property that the given property is constrained by
        ///     if the given property is part of a foreign key.
        /// </summary>
        /// <returns>The first associated principal property, or <see langword="null" /> if none exists.</returns>
        new IMutableProperty? FindFirstPrincipal()
            => (IMutableProperty?)((IReadOnlyProperty)this).FindFirstPrincipal();

        /// <summary>
        ///     Finds the list of principal properties including the given property that the given property is constrained by
        ///     if the given property is part of a foreign key.
        /// </summary>
        /// <returns>The list of all associated principal properties including the given property.</returns>
        new IReadOnlyList<IMutableProperty> GetPrincipals()
            => ((IReadOnlyProperty)this).GetPrincipals().Cast<IMutableProperty>().ToList();

        /// <summary>
        ///     Gets all foreign keys that use this property (including composite foreign keys in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The foreign keys that use this property.
        /// </returns>
        new IEnumerable<IMutableForeignKey> GetContainingForeignKeys();

        /// <summary>
        ///     Gets all indexes that use this property (including composite indexes in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The indexes that use this property.
        /// </returns>
        new IEnumerable<IMutableIndex> GetContainingIndexes();

        /// <summary>
        ///     Gets the primary key that uses this property (including a composite primary key in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The primary that use this property, or <see langword="null" /> if it is not part of the primary key.
        /// </returns>
        new IMutableKey? FindContainingPrimaryKey()
            => (IMutableKey?)((IReadOnlyProperty)this).FindContainingPrimaryKey();

        /// <summary>
        ///     Gets all primary or alternate keys that use this property (including composite keys in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The primary and alternate keys that use this property.
        /// </returns>
        new IEnumerable<IMutableKey> GetContainingKeys();

        /// <summary>
        ///     Sets the maximum length of data that is allowed in this property. For example, if the property is a <see cref="string" />
        ///     then this is the maximum number of characters.
        /// </summary>
        /// <param name="maxLength">The maximum length of data that is allowed in this property.</param>
        void SetMaxLength(int? maxLength);

        /// <summary>
        ///     Sets the precision of data that is allowed in this property.
        ///     For example, if the property is a <see cref="decimal" />
        ///     then this is the maximum number of digits.
        /// </summary>
        /// <param name="precision">The maximum number of digits that is allowed in this property.</param>
        void SetPrecision(int? precision);

        /// <summary>
        ///     Sets the scale of data that is allowed in this property.
        ///     For example, if the property is a <see cref="decimal" />
        ///     then this is the maximum number of decimal places.
        /// </summary>
        /// <param name="scale">The maximum number of decimal places that is allowed in this property.</param>
        void SetScale(int? scale);

        /// <summary>
        ///     Sets a value indicating whether this property can persist Unicode characters.
        /// </summary>
        /// <param name="unicode">
        ///     <see langword="true" /> if the property accepts Unicode characters, <see langword="false" /> if it does not,
        ///     <see langword="null" /> to clear the setting.
        /// </param>
        void SetIsUnicode(bool? unicode);

        /// <summary>
        ///     Gets or sets a value indicating whether this property can be modified before the entity is
        ///     saved to the database.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If <see cref="PropertySaveBehavior.Throw" />, then an exception
        ///         will be thrown if a value is assigned to this property when it is in
        ///         the <see cref="EntityState.Added" /> state.
        ///     </para>
        ///     <para>
        ///         If <see cref="PropertySaveBehavior.Ignore" />, then any value
        ///         set will be ignored when it is in the <see cref="EntityState.Added" /> state.
        ///     </para>
        /// </remarks>
        /// <param name="beforeSaveBehavior">
        ///     A value indicating whether this property can be modified before the entity is saved to the database.
        /// </param>
        void SetBeforeSaveBehavior(PropertySaveBehavior? beforeSaveBehavior);

        /// <summary>
        ///     Gets or sets a value indicating whether this property can be modified after the entity is
        ///     saved to the database.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If <see cref="PropertySaveBehavior.Throw" />, then an exception
        ///         will be thrown if a new value is assigned to this property after the entity exists in the database.
        ///     </para>
        ///     <para>
        ///         If <see cref="PropertySaveBehavior.Ignore" />, then any modification to the
        ///         property value of an entity that already exists in the database will be ignored.
        ///     </para>
        /// </remarks>
        /// <param name="afterSaveBehavior">
        ///     A value indicating whether this property can be modified after the entity is saved to the database.
        /// </param>
        void SetAfterSaveBehavior(PropertySaveBehavior? afterSaveBehavior);

        /// <summary>
        ///     Sets the factory to use for generating values for this property, or <see langword="null" /> to clear any previously set factory.
        /// </summary>
        /// <remarks>
        ///     Setting <see langword="null" /> does not disable value generation for this property, it just clears any generator explicitly
        ///     configured for this property. The database provider may still have a value generator for the property type.
        /// </remarks>
        /// <param name="valueGeneratorFactory">
        ///     A factory that will be used to create the value generator, or <see langword="null" /> to
        ///     clear any previously set factory.
        /// </param>
        void SetValueGeneratorFactory(Func<IProperty, IEntityType, ValueGenerator>? valueGeneratorFactory);

        /// <summary>
        ///     Sets the factory to use for generating values for this property, or <see langword="null" /> to clear any previously set factory.
        /// </summary>
        /// <remarks>
        ///     Setting <see langword="null" /> does not disable value generation for this property, it just clears any generator explicitly
        ///     configured for this property. The database provider may still have a value generator for the property type.
        /// </remarks>
        /// <param name="valueGeneratorFactory">
        ///     A factory that will be used to create the value generator, or <see langword="null" /> to
        ///     clear any previously set factory.
        /// </param>
        void SetValueGeneratorFactory(Type? valueGeneratorFactory);

        /// <summary>
        ///     Sets the custom <see cref="ValueConverter" /> for this property.
        /// </summary>
        /// <param name="converter">The converter, or <see langword="null" /> to remove any previously set converter.</param>
        void SetValueConverter(ValueConverter? converter);

        /// <summary>
        ///     Sets the custom <see cref="ValueConverter" /> for this property.
        /// </summary>
        /// <param name="converterType">
        ///     A type that derives from <see cref="ValueConverter" />, or <see langword="null" /> to remove any previously set converter.
        /// </param>
        void SetValueConverter(Type? converterType);

        /// <summary>
        ///     Sets the type that the property value will be converted to before being sent to the database provider.
        /// </summary>
        /// <param name="providerClrType">The type to use, or <see langword="null" /> to remove any previously set type.</param>
        void SetProviderClrType(Type? providerClrType);

        /// <summary>
        ///     Sets the <see cref="CoreTypeMapping" /> for the given property
        /// </summary>
        /// <param name="typeMapping">The <see cref="CoreTypeMapping" /> for this property.</param>
        void SetTypeMapping(CoreTypeMapping typeMapping);

        /// <summary>
        ///     Sets the custom <see cref="ValueComparer" /> for this property.
        /// </summary>
        /// <param name="comparer">The comparer, or <see langword="null" /> to remove any previously set comparer.</param>
        void SetValueComparer(ValueComparer? comparer);

        /// <summary>
        ///     Sets the custom <see cref="ValueComparer" /> for this property.
        /// </summary>
        /// <param name="comparerType">
        ///     A type that derives from <see cref="ValueComparer" />, or <see langword="null" /> to remove any previously set comparer.
        /// </param>
        void SetValueComparer(Type? comparerType);
    }
}
