using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CutList.Web.Tests.Infrastructure;

/// <summary>
/// Deterministic interleaving hook for tests. When armed, the next matching SaveChanges call
/// (by default: one that stages a Job, JobPart, or JobStock change) runs the supplied action
/// after the mutation has read and staged its changes but before any SQL is sent. The action
/// typically commits a competing change (such as locking the job) through a separate context.
/// </summary>
public sealed class MutationSaveGate : SaveChangesInterceptor
{
    private readonly object _sync = new();
    private Func<Task>? _action;
    private Func<DbContext, bool>? _predicate;

    public int TriggeredCount { get; private set; }

    public void BeforeNextSave(Func<Task> action, Func<DbContext, bool>? when = null)
    {
        lock (_sync)
        {
            if (_action != null)
                throw new InvalidOperationException("The save gate is already armed.");

            _action = action;
            _predicate = when ?? StagesJobAggregateChange;
        }
    }

    public bool IsArmed
    {
        get { lock (_sync) return _action != null; }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _action = null;
            _predicate = null;
            TriggeredCount = 0;
        }
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var action = TryTake(eventData.Context);
        if (action != null)
            await action();

        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var action = TryTake(eventData.Context);
        action?.Invoke().GetAwaiter().GetResult();
        return result;
    }

    private Func<Task>? TryTake(DbContext? context)
    {
        if (context == null)
            return null;

        lock (_sync)
        {
            if (_action == null || _predicate == null || !_predicate(context))
                return null;

            var action = _action;
            _action = null;
            _predicate = null;
            TriggeredCount++;
            return action;
        }
    }

    private static bool StagesJobAggregateChange(DbContext context) =>
        context.ChangeTracker.Entries()
            .Any(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                      && (e.Entity is Job or JobPart or JobStock));
}
