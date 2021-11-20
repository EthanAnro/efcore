// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
    ///         See <see href="https://aka.ms/efcore-docs-conventions">Model building conventions</see> for more information and examples.
    ///     </para>
    /// </remarks>
    public interface IConventionProperty : IReadOnlyProperty, IConventionPropertyBase
    {
        /// <summary>
        ///     Gets the builder that can be used to configure this property.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the property has been removed from the model.</exception>
        new IConventionPropertyBuilder Builder { get; }

        /// <summary>
        ///     Gets the type that this property belongs to.
        /// </summary>
        new IConventionEntityType DeclaringEntityType { get; }

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyPropertyBase.ClrType" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyPropertyBase.ClrType" />.</returns>
        ConfigurationSource? GetTypeConfigurationSource();

        /// <summary>
        ///     Sets a value indicating whether this property can contain <see langword="null" />.
        /// </summary>
        /// <param name="nullable">
        ///     A value indicating whether this property can contain <see langword="null" />.
        ///     <see langword="null" /> to reset to default.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        bool? SetIsNullable(bool? nullable, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.IsNullable" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.IsNullable" />.</returns>
        ConfigurationSource? GetIsNullableConfigurationSource();

        /// <summary>
        ///     Sets a value indicating when a value for this property will be generated by the database. Even when the
        ///     property is set to be generated by the database, EF may still attempt to save a specific value (rather than
        ///     having one generated by the database) when the entity is added and a value is assigned, or the property is
        ///     marked as modified for an existing entity. See <see cref="IReadOnlyProperty.GetBeforeSaveBehavior" /> and
        ///     <see cref="IReadOnlyProperty.GetAfterSaveBehavior" /> for more information and examples.
        /// </summary>
        /// <param name="valueGenerated">
        ///     A value indicating when a value for this property will be generated by the database.
        ///     <see langword="null" /> to reset to default.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        ValueGenerated? SetValueGenerated(ValueGenerated? valueGenerated, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.ValueGenerated" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.ValueGenerated" />.</returns>
        ConfigurationSource? GetValueGeneratedConfigurationSource();

        /// <summary>
        ///     Sets a value indicating whether this property is used as a concurrency token. When a property is configured
        ///     as a concurrency token the value in the database will be checked when an instance of this entity type
        ///     is updated or deleted during <see cref="DbContext.SaveChanges()" /> to ensure it has not changed since
        ///     the instance was retrieved from the database. If it has changed, an exception will be thrown and the
        ///     changes will not be applied to the database.
        /// </summary>
        /// <param name="concurrencyToken">
        ///     Sets a value indicating whether this property is used as a concurrency token.
        ///     <see langword="null" /> to reset to default.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        bool? SetIsConcurrencyToken(bool? concurrencyToken, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.IsConcurrencyToken" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.IsConcurrencyToken" />.</returns>
        ConfigurationSource? GetIsConcurrencyTokenConfigurationSource();

        /// <summary>
        ///     Returns a value indicating whether the property was created implicitly and isn't based on the CLR model.
        /// </summary>
        /// <returns>A value indicating whether the property was created implicitly and isn't based on the CLR model.</returns>
        bool IsImplicitlyCreated()
            => (IsShadowProperty() || (DeclaringEntityType.IsPropertyBag && IsIndexerProperty()))
                && GetConfigurationSource() == ConfigurationSource.Convention;

        /// <summary>
        ///     Finds the first principal property that the given property is constrained by
        ///     if the given property is part of a foreign key.
        /// </summary>
        /// <returns>The first associated principal property, or <see langword="null" /> if none exists.</returns>
        new IConventionProperty? FindFirstPrincipal()
            => (IConventionProperty?)((IReadOnlyProperty)this).FindFirstPrincipal();

        /// <summary>
        ///     Finds the list of principal properties including the given property that the given property is constrained by
        ///     if the given property is part of a foreign key.
        /// </summary>
        /// <returns>The list of all associated principal properties including the given property.</returns>
        new IReadOnlyList<IConventionProperty> GetPrincipals()
            => ((IReadOnlyProperty)this).GetPrincipals().Cast<IConventionProperty>().ToList();

        /// <summary>
        ///     Gets all foreign keys that use this property (including composite foreign keys in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The foreign keys that use this property.
        /// </returns>
        new IEnumerable<IConventionForeignKey> GetContainingForeignKeys();

        /// <summary>
        ///     Gets all indexes that use this property (including composite indexes in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The indexes that use this property.
        /// </returns>
        new IEnumerable<IConventionIndex> GetContainingIndexes();

        /// <summary>
        ///     Gets the primary key that uses this property (including a composite primary key in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The primary that use this property, or <see langword="null" /> if it is not part of the primary key.
        /// </returns>
        new IConventionKey? FindContainingPrimaryKey()
            => (IConventionKey?)((IReadOnlyProperty)this).FindContainingPrimaryKey();

        /// <summary>
        ///     Gets all primary or alternate keys that use this property (including composite keys in which this property
        ///     is included).
        /// </summary>
        /// <returns>
        ///     The primary and alternate keys that use this property.
        /// </returns>
        new IEnumerable<IConventionKey> GetContainingKeys();

        /// <summary>
        ///     Sets the <see cref="CoreTypeMapping" /> for the given property
        /// </summary>
        /// <param name="typeMapping">The <see cref="CoreTypeMapping" /> for this property.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        CoreTypeMapping? SetTypeMapping(CoreTypeMapping typeMapping, bool fromDataAnnotation = false);

        /// <summary>
        ///     Gets the <see cref="ConfigurationSource" /> for <see cref="CoreTypeMapping" /> of the property.
        /// </summary>
        /// <returns>The <see cref="ConfigurationSource" /> for <see cref="CoreTypeMapping" /> of the property.</returns>
        ConfigurationSource? GetTypeMappingConfigurationSource();

        /// <summary>
        ///     Sets the maximum length of data that is allowed in this property. For example, if the property is a <see cref="string" /> '
        ///     then this is the maximum number of characters.
        /// </summary>
        /// <param name="maxLength">The maximum length of data that is allowed in this property.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured property.</returns>
        int? SetMaxLength(int? maxLength, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetMaxLength" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetMaxLength" />.</returns>
        ConfigurationSource? GetMaxLengthConfigurationSource();

        /// <summary>
        ///     Sets the precision of data that is allowed in this property.
        ///     For example, if the property is a <see cref="decimal" />
        ///     then this is the maximum number of digits.
        /// </summary>
        /// <param name="precision">The maximum number of digits that is allowed in this property.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        int? SetPrecision(int? precision, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetPrecision" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetPrecision" />.</returns>
        ConfigurationSource? GetPrecisionConfigurationSource();

        /// <summary>
        ///     Sets the scale of data that is allowed in this property.
        ///     For example, if the property is a <see cref="decimal" />
        ///     then this is the maximum number of decimal places.
        /// </summary>
        /// <param name="scale">The maximum number of decimal places that is allowed in this property.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        int? SetScale(int? scale, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetScale" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetScale" />.</returns>
        ConfigurationSource? GetScaleConfigurationSource();

        /// <summary>
        ///     Sets a value indicating whether this property can persist Unicode characters.
        /// </summary>
        /// <param name="unicode">
        ///     <see langword="true" /> if the property accepts Unicode characters, <see langword="false" /> if it does not,
        ///     <see langword="null" /> to clear the setting.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        bool? SetIsUnicode(bool? unicode, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.IsUnicode" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.IsUnicode" />.</returns>
        ConfigurationSource? GetIsUnicodeConfigurationSource();

        /// <summary>
        ///     Sets a value indicating whether this property can be modified before the entity is
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
        ///     A value indicating whether this property can be modified before the entity is
        ///     saved to the database. <see langword="null" /> to reset to default.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        PropertySaveBehavior? SetBeforeSaveBehavior(PropertySaveBehavior? beforeSaveBehavior, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetBeforeSaveBehavior" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetBeforeSaveBehavior" />.</returns>
        ConfigurationSource? GetBeforeSaveBehaviorConfigurationSource();

        /// <summary>
        ///     Sets a value indicating whether this property can be modified after the entity is
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
        ///     Sets a value indicating whether this property can be modified after the entity is
        ///     saved to the database. <see langword="null" /> to reset to default.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        PropertySaveBehavior? SetAfterSaveBehavior(PropertySaveBehavior? afterSaveBehavior, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetAfterSaveBehavior" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetAfterSaveBehavior" />.</returns>
        ConfigurationSource? GetAfterSaveBehaviorConfigurationSource();

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
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        Func<IProperty, IEntityType, ValueGenerator>? SetValueGeneratorFactory(
            Func<IProperty, IEntityType, ValueGenerator>? valueGeneratorFactory,
            bool fromDataAnnotation = false);

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
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        Type? SetValueGeneratorFactory(
            Type? valueGeneratorFactory,
            bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetValueGeneratorFactory" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetValueGeneratorFactory" />.</returns>
        ConfigurationSource? GetValueGeneratorFactoryConfigurationSource();

        /// <summary>
        ///     Sets the custom <see cref="ValueConverter" /> for this property.
        /// </summary>
        /// <param name="converter">The converter, or <see langword="null" /> to remove any previously set converter.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        ValueConverter? SetValueConverter(ValueConverter? converter, bool fromDataAnnotation = false);

        /// <summary>
        ///     Sets the custom <see cref="ValueConverter" /> for this property.
        /// </summary>
        /// <param name="converterType">
        ///     A type that derives from <see cref="ValueConverter" />, or <see langword="null" /> to remove any previously set converter.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        Type? SetValueConverter(Type? converterType, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetValueConverter" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetValueConverter" />.</returns>
        ConfigurationSource? GetValueConverterConfigurationSource();

        /// <summary>
        ///     Sets the type that the property value will be converted to before being sent to the database provider.
        /// </summary>
        /// <param name="providerClrType">The type to use, or <see langword="null" /> to remove any previously set type.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        Type? SetProviderClrType(Type? providerClrType, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetProviderClrType" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetProviderClrType" />.</returns>
        ConfigurationSource? GetProviderClrTypeConfigurationSource();

        /// <summary>
        ///     Sets the custom <see cref="ValueComparer" /> for this property.
        /// </summary>
        /// <param name="comparer">The comparer, or <see langword="null" /> to remove any previously set comparer.</param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        ValueComparer? SetValueComparer(ValueComparer? comparer, bool fromDataAnnotation = false);

        /// <summary>
        ///     Sets the custom <see cref="ValueComparer" /> for this property.
        /// </summary>
        /// <param name="comparerType">
        ///     A type that derives from <see cref="ValueComparer" />, or <see langword="null" /> to remove any previously set comparer.
        /// </param>
        /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
        /// <returns>The configured value.</returns>
        Type? SetValueComparer(Type? comparerType, bool fromDataAnnotation = false);

        /// <summary>
        ///     Returns the configuration source for <see cref="IReadOnlyProperty.GetValueComparer" />.
        /// </summary>
        /// <returns>The configuration source for <see cref="IReadOnlyProperty.GetValueComparer" />.</returns>
        ConfigurationSource? GetValueComparerConfigurationSource();
    }
}
