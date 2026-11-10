namespace Archiving.Application.Common.Interfaces;

/// <summary>
/// Turns workflow state into internal correspondence.
///
/// <para>The engine decides <i>who is next</i>; this decides <i>how they hear about it</i> — by
/// generating a real message in المراسلات, threaded under the request the employee composed, so the
/// approver meets the task in the inbox they already use rather than in a second one built for
/// workflow. There is no separate messaging system anywhere in this feature.</para>
///
/// <para>Generated messages deliberately do <b>not</b> go through <c>IInternalMailService.ComposeAsync</c>:
/// composing is what <i>starts</i> a workflow, so routing through it would have every step start
/// another run. The router writes the correspondence rows directly, which also keeps the dependency
/// one-way — mail knows about the engine, the engine does not know about mail.</para>
/// </summary>
public interface IWorkflowMailRouter
{
    /// <summary>Puts a freshly opened task in front of its assignee as internal correspondence.
    /// A task with no resolvable recipient produces no message and is left to escalation.</summary>
    Task<long?> SendStepAsync(long instanceId, long taskId, CancellationToken ct = default);

    /// <summary>Sends the closing message — approved, rejected, or returned for changes — to the
    /// employee who raised the request, on the same thread.</summary>
    Task<long?> SendResultAsync(long instanceId, WorkflowOutcome outcome, string? reason, CancellationToken ct = default);
}

/// <summary>How a run ended, from the requester's point of view.</summary>
public enum WorkflowOutcome
{
    Approved = 0,
    Rejected = 1,
    ChangesRequested = 2
}
