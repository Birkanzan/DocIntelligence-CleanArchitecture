using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DocIntelligence.Application.Mapping;

namespace DocIntelligence.Application;

/// <summary>
/// Application katmanının DI kayıtları.
/// WebAPI ve Worker bu metodu çağırır.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceRegistration).Assembly;

        // MediatR - CQRS
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // AutoMapper
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // IHttpContextAccessor (WebAPI register edecek)

        return services;
    }
}
