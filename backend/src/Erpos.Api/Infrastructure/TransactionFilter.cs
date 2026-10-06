using Erpos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Api.Infrastructure;

/// <summary>
/// Runs every write request (POST/PUT/PATCH/DELETE) in one database transaction. Module operations chain several saves
/// (invoice → approval → receipt → source record); either all of them commit or, on any error, none do. Combined with the
/// concurrency tokens on statuses and balances, a second simultaneous click fails cleanly instead of posting twice.
/// </summary>
public class TransactionFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method) || db.Database.CurrentTransaction != null
            || context.ActionDescriptor.EndpointMetadata.OfType<NoTransactionAttribute>().Any())
        {
            await next();
            return;
        }

        // Read committed: after waiting on a row lock, later reads see what the other request just committed.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, context.HttpContext.RequestAborted);
        var executed = await next();
        var failed = executed.Exception != null && !executed.ExceptionHandled
                     || executed.Result is IStatusCodeActionResult { StatusCode: >= 400 };
        if (failed) await tx.RollbackAsync();
        else await tx.CommitAsync();
    }
}

/// <summary>Opts an endpoint out of the request transaction (it must persist something even when it answers with an error).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class NoTransactionAttribute : Attribute;
