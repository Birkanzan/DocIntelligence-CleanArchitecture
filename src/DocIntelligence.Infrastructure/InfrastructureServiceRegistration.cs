using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Messaging;
using DocIntelligence.Infrastructure.Options;
using DocIntelligence.Infrastructure.Persistence;
using DocIntelligence.Infrastructure.Repositories;
using DocIntelligence.Infrastructure.Services;
using DocIntelligence.Infrastructure.Storage;

namespace DocIntelligence.Infrastructure;

/// <summary>
/// Infrastructure katmanının DI kayıtları.
/// Tüm interface → implementasyon bağlamaları burada yapılır.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- EF Core ---
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions
                    .EnableRetryOnFailure(maxRetryCount: 5)
                    .CommandTimeout(30)));

        // --- Options ---
        services.Configure<LocalStorageOptions>(
            configuration.GetSection(LocalStorageOptions.SectionName));
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<TesseractOptions>(
            configuration.GetSection(TesseractOptions.SectionName));
        services.Configure<MlOptions>(
            configuration.GetSection(MlOptions.SectionName));

        // --- Repositories ---
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // --- Storage ---
        services.AddSingleton<IStorageService, LocalStorageService>();

        // --- Messaging ---
        services.AddSingleton<IMessageBroker, RabbitMqMessageBroker>();

        // --- Services ---
        services.AddScoped<IOcrService, TesseractOcrService>();
        services.AddScoped<IDocumentOptimizer, DocumentOptimizer>();
        services.AddSingleton<IDocumentClassifier, MlDocumentClassifier>();

        return services;
    }
}
