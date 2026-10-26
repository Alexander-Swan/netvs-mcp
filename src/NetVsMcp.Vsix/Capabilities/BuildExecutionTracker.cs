using System;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class BuildExecutionTracker
{
    internal sealed class Ticket(string configuration, string platform, string? project, string scope, string action)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Configuration { get; } = configuration;
        public string Platform { get; } = platform;
        public string? Project { get; } = project;
        public string Scope { get; } = scope;
        public string Action { get; } = action;
        public bool Begun;
        public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? Completed;
        public int? Errors;
        public TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly object gate = new();
    private Ticket? active, last;

    public Ticket Begin(string configuration, string platform, string? project, string scope, string action)
    {
        lock (gate)
        {
            if (active is not null) throw new InvalidOperationException("An earlier build is still active. Inspect its buildId and use build_cancel if cancellation is intended.");
            return active = new(configuration, platform, project, scope, action);
        }
    }

    public void ObserveBegin(string scope, string action)
    {
        lock (gate)
        {
            last = null; // An external build invalidates an earlier completed identity.
            if (active is null) return;
            if (active.Begun || active.Scope != scope || active.Action != action)
            {
                active.Completion.TrySetException(new InvalidOperationException("Another build interfered with the requested build; reconcile current state before retrying."));
                active = null;
            }
            else active.Begun = true;
        }
    }

    public void ObserveDone(string scope, string action, int errors)
    {
        lock (gate)
        {
            if (active is null || !active.Begun || active.Scope != scope || active.Action != action) return;
            active.Errors = errors;
            active.Completed = DateTimeOffset.UtcNow;
            active.Completion.TrySetResult(errors);
            last = active;
            active = null;
        }
    }

    public Ticket? Current { get { lock (gate) return active ?? last; } }

    public void Abort(Ticket ticket)
    {
        lock (gate)
        {
            if (ReferenceEquals(active, ticket)) active = null;
            ticket.Completion.TrySetCanceled();
        }
    }
}
