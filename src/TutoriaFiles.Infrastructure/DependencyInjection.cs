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
        Console.WriteLine("  [Infrastructure] Starting service registration...");

        try
        {
            // Register DbContext
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("  [Infrastructure] Registering TutoriaDbContext...");
                services.AddDbContext<TutoriaDbContext>(options =>
                    options.UseSqlServer(connectionString));
                Console.WriteLine("  [OK] Registered: TutoriaDbContext");
            }
            else
            {
                Console.WriteLine("  [SKIP] TutoriaDbContext - No connection string configured");
            }

            // Auto-register all repositories
            Console.WriteLine("  [Infrastructure] Auto-registering repositories...");
            services.AddRepositories();

            // Auto-register all services
            Console.WriteLine("  [Infrastructure] Auto-registering services...");
            services.AddServices();

            // Register helpers
            services.AddScoped<AccessControlHelper>();
            Console.WriteLine("  [OK] Registered: AccessControlHelper");

            Console.WriteLine("  [Infrastructure] Service registration complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [ERROR] Infrastructure registration failed: {ex.Message}");
            Console.WriteLine($"  [ERROR] Exception type: {ex.GetType().FullName}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  [ERROR] Inner exception: {ex.InnerException.Message}");
            }
            throw; // Re-throw to propagate to Program.cs catch block
        }

        return services;
    }

    /// <summary>
    /// Automatically registers all service implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Services
    /// </summary>
    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        try
        {
            var infrastructureAssembly = Assembly.GetExecutingAssembly();
            Console.WriteLine($"    Loading TutoriaFiles.Core assembly...");
            var coreAssembly = Assembly.Load("TutoriaFiles.Core");

            // Get all service interfaces from Core (IBlobStorageService, etc.)
            var serviceInterfaces = coreAssembly.GetTypes()
                .Where(t => t.IsInterface && t.Name.EndsWith("Service"))
                .ToList();

            Console.WriteLine($"    Found {serviceInterfaces.Count} service interface(s) in Core");

            // Get all service implementations from Infrastructure
            var serviceImplementations = infrastructureAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"))
                .ToList();

            Console.WriteLine($"    Found {serviceImplementations.Count} service implementation(s) in Infrastructure");

            var registeredCount = 0;
            foreach (var interfaceType in serviceInterfaces)
            {
                // Find the matching implementation
                var implementation = serviceImplementations
                    .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

                if (implementation != null)
                {
                    services.AddScoped(interfaceType, implementation);
                    Console.WriteLine($"    [OK] Registered: {interfaceType.Name} -> {implementation.Name}");
                    registeredCount++;
                }
                else
                {
                    Console.WriteLine($"    [WARN] No implementation found for: {interfaceType.Name}");
                }
            }

            Console.WriteLine($"    Services registered: {registeredCount}/{serviceInterfaces.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERROR] Failed to register services: {ex.Message}");
            throw;
        }

        return services;
    }

    /// <summary>
    /// Automatically registers all repository implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Repositories
    /// </summary>
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        try
        {
            var infrastructureAssembly = Assembly.GetExecutingAssembly();
            var coreAssembly = Assembly.Load("TutoriaFiles.Core");

            // Get all repository interfaces from Core (IFileRepository, IModuleRepository, etc.)
            var repositoryInterfaces = coreAssembly.GetTypes()
                .Where(t => t.IsInterface && t.Name.EndsWith("Repository"))
                .ToList();

            Console.WriteLine($"    Found {repositoryInterfaces.Count} repository interface(s) in Core");

            // Get all repository implementations from Infrastructure
            var repositoryImplementations = infrastructureAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Repository"))
                .ToList();

            Console.WriteLine($"    Found {repositoryImplementations.Count} repository implementation(s) in Infrastructure");

            var registeredCount = 0;
            foreach (var interfaceType in repositoryInterfaces)
            {
                // Find the matching implementation
                var implementation = repositoryImplementations
                    .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

                if (implementation != null)
                {
                    services.AddScoped(interfaceType, implementation);
                    Console.WriteLine($"    [OK] Registered: {interfaceType.Name} -> {implementation.Name}");
                    registeredCount++;
                }
                else
                {
                    Console.WriteLine($"    [WARN] No implementation found for: {interfaceType.Name}");
                }
            }

            Console.WriteLine($"    Repositories registered: {registeredCount}/{repositoryInterfaces.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERROR] Failed to register repositories: {ex.Message}");
            throw;
        }

        return services;
    }
}
