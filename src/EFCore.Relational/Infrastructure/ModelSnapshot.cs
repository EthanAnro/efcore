// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    ///     Base class for the snapshot of the <see cref="IModel" /> state generated by Migrations.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-migrations">Database migrations</see> for more information and examples.
    /// </remarks>
    public abstract class ModelSnapshot
    {
        private IModel? _model;

        private IModel CreateModel()
        {
            var modelBuilder = new ModelBuilder();

            BuildModel(modelBuilder);

            return (IModel)modelBuilder.Model;
        }

        /// <summary>
        ///     The snapshot model.
        /// </summary>
        public virtual IModel Model
            => _model ??= CreateModel();

        /// <summary>
        ///     Called lazily by <see cref="Model" /> to build the model snapshot
        ///     the first time it is requested.
        /// </summary>
        /// <param name="modelBuilder">The <see cref="ModelBuilder" /> to use to build the model.</param>
        protected abstract void BuildModel(ModelBuilder modelBuilder);
    }
}
