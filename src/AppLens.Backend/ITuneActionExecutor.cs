namespace AppLens.Backend;

// Shared approval-loop boundary. The deferred Windows Tune implementation lives outside core.
public interface ITuneActionExecutor
{
    Task<TuneActionRecord> ExecuteAsync(TunePlanItem item, bool userApproved, CancellationToken cancellationToken = default);
}
