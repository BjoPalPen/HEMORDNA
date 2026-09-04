using Hemordna.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hemordna.Api;

/// <summary>
/// Turns domain rule violations into meaningful HTTP responses instead of 500s.
/// </summary>
/// <remarks>
/// A broken invariant is the caller asking for something the household's rules do not allow -
/// a duplicate member name, deferring a task that cannot be deferred. That is a conflict,
/// not a server fault. Unknown exceptions are deliberately not handled here: they should
/// surface as 500 and be logged, not be quietly reshaped into a 4xx.
/// </remarks>
internal sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status409Conflict, "Åtgärden strider mot hushållets regler."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Ogiltig begäran."),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            return false;
        }

        _logger.LogInformation(
            "Rejected {Method} {Path}: {Reason}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
