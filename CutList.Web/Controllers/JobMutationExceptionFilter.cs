using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CutList.Web.Controllers;

/// <summary>
/// Maps the job-domain conflicts raised by <see cref="JobService"/> to stable 409 problem responses.
/// All other exceptions are left to the normal pipeline.
/// </summary>
public sealed class JobMutationExceptionFilter : IExceptionFilter
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public JobMutationExceptionFilter(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = problemDetailsFactory;
    }

    public void OnException(ExceptionContext context)
    {
        ProblemDetails? problem = context.Exception switch
        {
            JobLockedException locked => Create(context, "Job is locked", locked.Message, "job_locked", locked.JobId,
                ("lockedAt", locked.LockedAt)),
            JobMutationConflictException changed => Create(context, "Job changed", changed.Message, "job_changed",
                changed.JobId),
            _ => null
        };

        if (problem == null)
            return;

        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        context.ExceptionHandled = true;
    }

    private ProblemDetails Create(ExceptionContext context, string title, string detail, string code, int jobId,
        params (string Key, object Value)[] extensions)
    {
        var problem = _problemDetailsFactory.CreateProblemDetails(
            context.HttpContext, StatusCodes.Status409Conflict, title, detail: detail);
        problem.Extensions["code"] = code;
        problem.Extensions["jobId"] = jobId;
        foreach (var (key, value) in extensions)
            problem.Extensions[key] = value;
        return problem;
    }
}
