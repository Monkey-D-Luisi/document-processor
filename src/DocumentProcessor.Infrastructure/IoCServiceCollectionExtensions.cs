using System.Reflection;
using DocumentProcessor.Application.Common.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessor.Infrastructure;

public static class IoCServiceCollectionExtensions
{
    public static IServiceCollection AddAttributedServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0 || assemblies.Any(assembly => assembly is null))
            throw new ArgumentException("At least one non-null assembly is required.", nameof(assemblies));

        var implementationTypes = assemblies
            .Distinct()
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(assembly => assembly.DefinedTypes
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => type.AsType()))
            .ToArray();

        return AddAttributedTypes(services, implementationTypes);
    }

    internal static IServiceCollection AddAttributedTypes(
        IServiceCollection services,
        IEnumerable<Type> implementationTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationTypes);

        var declarations = implementationTypes
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .SelectMany(type => type.GetCustomAttributes<IoCAttribute>(inherit: false)
                .OrderBy(attribute => attribute.ServiceType.FullName, StringComparer.Ordinal)
                .Select(attribute => new IoCDeclaration(type, attribute)))
            .ToArray();

        ValidateDeclarations(services, declarations);

        foreach (var implementationGroup in declarations.GroupBy(declaration => declaration.Implementation))
        {
            var implementation = implementationGroup.Key;
            var lifetime = ConvertLifetime(implementationGroup.First().Attribute.Lifetime);
            if (!services.Any(descriptor => descriptor.ServiceType == implementation))
                services.Add(ServiceDescriptor.Describe(implementation, implementation, lifetime));
        }

        foreach (var declaration in declarations)
        {
            if (declaration.Attribute.ServiceType == declaration.Implementation)
                continue;

            var lifetime = ConvertLifetime(declaration.Attribute.Lifetime);
            services.Add(ServiceDescriptor.Describe(
                declaration.Attribute.ServiceType,
                provider => provider.GetRequiredService(declaration.Implementation),
                lifetime));
        }

        return services;
    }

    private static void ValidateDeclarations(
        IServiceCollection services,
        IReadOnlyCollection<IoCDeclaration> declarations)
    {
        foreach (var declaration in declarations)
        {
            var implementation = declaration.Implementation;
            var service = declaration.Attribute.ServiceType;
            if (!implementation.IsClass || implementation.IsAbstract || implementation.ContainsGenericParameters ||
                service.ContainsGenericParameters || (!service.IsInterface && !service.IsClass) ||
                !service.IsAssignableFrom(implementation) || !Enum.IsDefined(declaration.Attribute.Lifetime))
            {
                throw new InvalidOperationException(
                    $"Invalid [IoC] declaration: {implementation.FullName} -> {service.FullName} " +
                    $"({declaration.Attribute.Lifetime}). A closed, concrete implementation of the declared " +
                    "service is required.");
            }
        }

        var duplicateDeclaration = declarations
            .GroupBy(declaration => (declaration.Implementation, declaration.Attribute.ServiceType))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDeclaration is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate [IoC] declaration: {duplicateDeclaration.Key.Implementation.FullName} -> " +
                duplicateDeclaration.Key.ServiceType.FullName);
        }

        foreach (var serviceGroup in declarations.GroupBy(declaration => declaration.Attribute.ServiceType))
        {
            if (serviceGroup.Count() > 1 && serviceGroup.Any(declaration => !declaration.Attribute.AllowMultiple))
            {
                throw new InvalidOperationException(
                    $"Duplicate single [IoC] service {serviceGroup.Key.FullName}: " +
                    string.Join(", ", serviceGroup.Select(declaration => declaration.Implementation.FullName)));
            }

            if (serviceGroup.Select(declaration => declaration.Attribute.Lifetime).Distinct().Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Conflicting [IoC] lifetimes for service {serviceGroup.Key.FullName}.");
            }

            if (services.Any(descriptor => descriptor.ServiceType == serviceGroup.Key))
            {
                throw new InvalidOperationException(
                    $"The [IoC] service {serviceGroup.Key.FullName} is already registered explicitly.");
            }
        }

        foreach (var implementationGroup in declarations.GroupBy(declaration => declaration.Implementation))
        {
            if (implementationGroup.Select(declaration => declaration.Attribute.Lifetime).Distinct().Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Conflicting [IoC] lifetimes for implementation {implementationGroup.Key.FullName}.");
            }

            var implementation = implementationGroup.Key;
            var existing = services
                .Where(descriptor => descriptor.ServiceType == implementation)
                .ToArray();
            var lifetime = ConvertLifetime(implementationGroup.First().Attribute.Lifetime);
            if (existing.Length > 1 || existing.Any(descriptor => descriptor.Lifetime != lifetime))
            {
                throw new InvalidOperationException(
                    $"Conflicting explicit construction for [IoC] implementation {implementation.FullName}.");
            }
        }
    }

    private static ServiceLifetime ConvertLifetime(IoCLifetime lifetime) => lifetime switch
    {
        IoCLifetime.Transient => ServiceLifetime.Transient,
        IoCLifetime.Scoped => ServiceLifetime.Scoped,
        IoCLifetime.Singleton => ServiceLifetime.Singleton,
        _ => throw new InvalidOperationException("Invalid [IoC] lifetime."),
    };

    private sealed record IoCDeclaration(Type Implementation, IoCAttribute Attribute);
}
