using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WaterbusSystem.Application.Common.Behaviors;

namespace WaterbusSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Đăng ký MediatR xử lý CQRS trong toàn bộ Application assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Gắn Middleware Pipeline Behaviors
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Đăng ký toàn bộ Validators của FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
