using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Domain.Exceptions;

namespace WaterbusSystem.WebApi.Middlewares;

/// <summary>
/// Middleware xử lý ngoại lệ tập trung, trả về định dạng ProblemDetails chuẩn RFC 7807
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UNHANDLED EXCEPTION] Lỗi phát sinh tại {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };

        switch (exception)
        {
            case ValidationException validationException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Dữ liệu yêu cầu không hợp lệ.";
                problemDetails.Detail = "Vui lòng kiểm tra lại các trường thông tin.";
                problemDetails.Extensions["errors"] = validationException.Errors;
                break;

            case ConcurrencyException concurrencyException:
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                problemDetails.Status = (int)HttpStatusCode.Conflict;
                problemDetails.Title = "Xung đột dữ liệu giữ chỗ / Double-booking.";
                problemDetails.Detail = concurrencyException.Message;
                break;

            case NotFoundException notFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                problemDetails.Status = (int)HttpStatusCode.NotFound;
                problemDetails.Title = "Không tìm thấy dữ liệu.";
                problemDetails.Detail = notFoundException.Message;
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                problemDetails.Title = "Đã xảy ra lỗi máy chủ nội bộ.";
                problemDetails.Detail = "Hệ thống đang xử lý sự cố. Vui lòng thử lại sau ít phút.";
                break;
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
