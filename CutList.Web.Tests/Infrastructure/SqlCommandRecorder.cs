using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CutList.Web.Tests.Infrastructure;

/// <summary>
/// Records SQL sent by the test host while enabled, and can inject a failure at the end of one
/// matching command batch (after its statements have executed) to prove transactional rollback.
/// </summary>
public sealed class SqlCommandRecorder : DbCommandInterceptor
{
    private readonly object _sync = new();
    private readonly List<string> _commands = new();
    private bool _recording;
    private Func<string, bool>? _faultWhen;
    private string? _faultEvidenceSql;

    public IReadOnlyList<string> Commands
    {
        get { lock (_sync) return _commands.ToList(); }
    }

    /// <summary>The command text that received the injected failure, if any.</summary>
    public string? FaultedCommand { get; private set; }

    public void Start()
    {
        lock (_sync)
        {
            _commands.Clear();
            _recording = true;
        }
    }

    /// <summary>
    /// Appends a THROW to the next reader command whose text satisfies <paramref name="when"/>, so
    /// SQL Server executes the batch's writes and then fails. <paramref name="evidenceSql"/> is an
    /// optional scalar T-SQL expression evaluated just before the THROW and included in its message
    /// (for example, a count proving the batch's writes had executed).
    /// </summary>
    public void FailAfterNextBatch(Func<string, bool> when, string? evidenceSql = null)
    {
        lock (_sync)
        {
            _faultWhen = when;
            _faultEvidenceSql = evidenceSql;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _commands.Clear();
            _recording = false;
            _faultWhen = null;
            FaultedCommand = null;
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Observe(command, allowFault: true);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Observe(command, allowFault: true);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Observe(command, allowFault: false);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Observe(command, allowFault: false);
        return ValueTask.FromResult(result);
    }

    private void Observe(DbCommand command, bool allowFault)
    {
        lock (_sync)
        {
            if (_recording)
                _commands.Add(command.CommandText);

            if (allowFault && _faultWhen != null && _faultWhen(command.CommandText))
            {
                _faultWhen = null;
                var evidence = _faultEvidenceSql ?? "N''";
                _faultEvidenceSql = null;
                command.CommandText +=
                    "\nDECLARE @injectedFailure nvarchar(2048) = CONCAT(N'Injected failure after job writes; ', " +
                    evidence + ", N'; trancount=', @@TRANCOUNT);" +
                    "\nTHROW 50001, @injectedFailure, 1;";
                FaultedCommand = command.CommandText;
            }
        }
    }
}
