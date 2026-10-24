using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = WaterbusSystem.Application.Common.Exceptions.ValidationException;

namespace WaterbusSystem.WebApi.Filters;

/// <summary>
/// Validate action argument bằng FluentValidation (IValidator&lt;T&gt; tương ứng, resolve từ DI).
/// Thay thế ValidationBehavior của MediatR cho các Controller đã chuyển sang Service pattern (Clean Architecture thuần).
/// Gắn thủ công qua [ServiceFilter(typeof(ValidateRequestFilter))] trên từng Controller/action đã migrate,
/// KHÔNG đăng ký global để tránh double-validate các Controller CQRS/MediatR còn lại.
/// </summary>
public class ValidateRequestFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidateRequestFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
