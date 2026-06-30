using Microsoft.Extensions.DependencyInjection;

namespace FinControl.Infrastructure.Repositories;

/// <summary>
/// Extension methods for registering generic repositories in the DI container.
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Registers a generic repository for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRepository<TEntity>(this IServiceCollection services)
        where TEntity : class
    {
        services.AddScoped<IRepository<TEntity>, Repository<TEntity>>();
        return services;
    }

    /// <summary>
    /// Registers multiple generic repositories at once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="entityTypes">The entity types to register repositories for.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRepositories(this IServiceCollection services, params Type[] entityTypes)
    {
        foreach (var entityType in entityTypes)
        {
            if (!entityType.IsClass || entityType.IsAbstract)
                throw new ArgumentException($"Type '{entityType.Name}' must be a concrete class.", nameof(entityTypes));

            var interfaceType = typeof(IRepository<>).MakeGenericType(entityType);
            var repositoryType = typeof(Repository<>).MakeGenericType(entityType);

            services.AddScoped(interfaceType, repositoryType);
        }

        return services;
    }
}
