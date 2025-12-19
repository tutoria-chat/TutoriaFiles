using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutoriaFiles.Infrastructure.Data;
using TutoriaFiles.Infrastructure.Helpers;

namespace TutoriaFiles.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure services including DbContext, Repositories, and Services
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<TutoriaDbContext>(options =>
                options.UseSqlServer(connectionString));
            Console.WriteLine("✓ Registered: TutoriaDbContext");
        }

        // Auto-register all repositories
        services.AddRepositories();

        // Auto-register all services
        services.AddServices();

        // Register helpers
        services.AddScoped<AccessControlHelper>();
        Console.WriteLine("✓ Registered: AccessControlHelper");

        return services;
    }

    /// <summary>
    /// Automatically registers all service implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Services
    /// </summary>
    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        var infrastructureAssembly = Assembly.GetExecutingAssembly();
        var coreAssembly = Assembly.Load("TutoriaFiles.Core");

        // Get all service interfaces from Core (IBlobStorageService, etc.)
        var serviceInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Service"))
            .ToList();

        // Get all service implementations from Infrastructure
        var serviceImplementations = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"))
            .ToList();

        foreach (var interfaceType in serviceInterfaces)
        {
            // Find the matching implementation
            var implementation = serviceImplementations
                .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

            if (implementation != null)
            {
                services.AddScoped(interfaceType, implementation);
                Console.WriteLine($"✓ Registered: {interfaceType.Name} → {implementation.Name}");
            }
        }

        return services;
    }

    /// <summary>
    /// Automatically registers all repository implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Repositories
    /// </summary>
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        var infrastructureAssembly = Assembly.GetExecutingAssembly();
        var coreAssembly = Assembly.Load("TutoriaFiles.Core");

        // Get all repository interfaces from Core (IFileRepository, IModuleRepository, etc.)
        var repositoryInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository"))
            .ToList();

        // Get all repository implementations from Infrastructure
        var repositoryImplementations = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Repository"))
            .ToList();

        foreach (var interfaceType in repositoryInterfaces)
        {
            // Find the matching implementation
            var implementation = repositoryImplementations
                .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

            if (implementation != null)
            {
                services.AddScoped(interfaceType, implementation);
                Console.WriteLine($"✓ Registered: {interfaceType.Name} → {implementation.Name}");
            }
        }

        return services;
    }
}
