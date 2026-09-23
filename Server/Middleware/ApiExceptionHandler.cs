using AGUIWebChat.Server.Services.AI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AGUIWebChat.Server.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ModelValidationException => (400, "Invalid model", exception.Message),
            ModelNotFoundException => (404, "Model not found", exception.Message),
            ModelConflictException => (409, "Model conflict", exception.Message),
            BadHttpRequestException badRequest => (badRequest.StatusCode, "Invalid request", "The request could not be read."),
            _ => (500, "Internal server error", "An unexpected error occurred.")
        };

        if (status >= 500) logger.LogError(exception, "API request failed: {TraceId}", context.TraceIdentifier);

        ProblemDetails problem = exception is ModelValidationException
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["model"] = [detail] })
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Instance = context.Request.Path;
        problem.Type = "about:blank";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;

        if (problem is HttpValidationProblemDetails validation)
            await context.Response.WriteAsJsonAsync(validation, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        else
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
