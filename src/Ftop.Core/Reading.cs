// A reading is either a value or "unavailable with a reason".
// Mirrors ftop's rule: a reading the hardware does not give is shown
// as unavailable, never as zero.
namespace Ftop.Core;

public readonly record struct Reading<T>(T Value, bool IsAvailable, string? UnavailableReason)
{
    public static Reading<T> Ok(T value) => new(value, true, null);
    public static Reading<T> Unavailable(string reason) => new(default!, false, reason);
}
