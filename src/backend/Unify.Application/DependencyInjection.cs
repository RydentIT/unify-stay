using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Messaging;
using Unify.Application.Messaging.Behaviors;

namespace Unify.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dispatcher, every request handler and every validator found in this
    /// assembly. Adding a use case therefore needs no DI edit - only the handler class.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.TryAddScoped<IDispatcher, Dispatcher>();

        // Outermost behaviour first. Validation runs before any handler sees the request.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        AddImplementationsOfOpenInterface(services, assembly, typeof(IRequestHandler<,>));
        AddImplementationsOfOpenInterface(services, assembly, typeof(IValidator<>));

        return services;
    }

    private static void AddImplementationsOfOpenInterface(
        IServiceCollection services,
        Assembly assembly,
        Type openInterface)
    {
        IEnumerable<Type> candidates = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (Type implementation in candidates)
        {
            IEnumerable<Type> closedInterfaces = implementation
                .GetInterfaces()
                .Where(contract =>
                    contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == openInterface);

            foreach (Type contract in closedInterfaces)
            {
                services.AddScoped(contract, implementation);
            }
        }
    }
}
