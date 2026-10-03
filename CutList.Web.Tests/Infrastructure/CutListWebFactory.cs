using CutList.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests.Infrastructure;

/// <summary>
/// Hosts the real CutList.Web application in-process, replacing only the ApplicationDbContext
/// provider registration so every request and service call uses the disposable test database.
/// </summary>
public sealed class CutListWebFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CutListWebFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public MutationSaveGate SaveGate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove every production ApplicationDbContext registration (factory, options,
            // options configuration, scoped context) before adding the test database.
            var productionRegistrations = services
                .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                            || (d.ServiceType.IsGenericType
                                && d.ServiceType.GenericTypeArguments.Contains(typeof(ApplicationDbContext))))
                .ToList();
            foreach (var descriptor in productionRegistrations)
                services.Remove(descriptor);

            services.AddDbContextFactory<ApplicationDbContext>(options => options
                .UseSqlServer(_connectionString)
                .AddInterceptors(SaveGate));
        });
    }
}
