using Erpos.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Infrastructure;

/// <summary>Turns application exceptions into RFC 7807 problem responses with a readable message.</summary>
public class ExceptionHandler(ILogger<ExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            AppException app => (app.StatusCode, app.Message),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (409, "This record was changed by someone else at the same moment. Please reload and try again."),
            _ when IsLockConflict(ex) => (409, "Someone else was saving related records at the same moment. Please try again."),
            _ => (500, "An unexpected error occurred.")
        };
        // The trace id ties what the user reports ("error 7c1f…") to the server log line.
        var traceId = System.Diagnostics.Activity.Current?.Id ?? ctx.TraceIdentifier;
        if (status == 500) logger.LogError(ex, "Unhandled exception {TraceId}", traceId);

        ctx.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 && env.IsDevelopment() ? ex.ToString() : null
        };
        problem.Extensions["traceId"] = traceId;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    /// <summary>MySQL deadlock (1213) or lock-wait timeout (1205), possibly wrapped by EF.</summary>
    private static bool IsLockConflict(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is MySql.Data.MySqlClient.MySqlException { Number: 1213 or 1205 }) return true;
        return false;
    }
}
