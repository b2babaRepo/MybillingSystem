using System.Collections.Concurrent;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// Wakes shop apps waiting for news the moment an order reaches their shop, so a new order shows within a second
/// instead of at the next 30-second check. One process only: with several servers this needs a shared channel
/// (for example Postgres LISTEN/NOTIFY); waiting apps still fall back to their timeout and re-check.
/// </summary>
public static class OrderSignals
{
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Waiting = new();

    /// <summary>Completes when <see cref="Notify"/> is next called for the shop.</summary>
    public static Task Next(Guid shopId) =>
        Waiting.GetOrAdd(shopId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    /// <summary>Tells every app waiting on the shop that something changed.</summary>
    public static void Notify(Guid shopId)
    {
        if (Waiting.TryRemove(shopId, out var signal))
        {
            signal.TrySetResult();
        }
    }
}
