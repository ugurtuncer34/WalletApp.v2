using System.Net;
using WalletApp.Dtos;

namespace WalletApp.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, IHostEnvironment env, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // pass request to next step, like controller
        }
        catch (Exception ex)
        {
            // if throw exception, catch in the air. names will be detected by Serilog
            _logger.LogError(ex, "An unexpected error occured. Path: {RequestPath}, Message: {ErrorMessage}", context.Request.Path, ex.Message);
            await HandleExceptionAsync(context, ex, _env);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, IHostEnvironment env)
    {
        context.Response.ContentType = "application/json";
        // deciding the return according to the type of exception
        context.Response.StatusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var response = new ErrorResponse
        {
            StatusCode = context.Response.StatusCode,
            Message = exception.Message,
            Details = env.IsDevelopment() ? exception.StackTrace?.ToString() : "An unexpected error occured."
        };

        return context.Response.WriteAsync(response.ToString());
    }
}