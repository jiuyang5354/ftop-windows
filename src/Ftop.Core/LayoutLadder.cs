// Any-size layout, mirroring ftop's LayoutLadder.
// The ladder is pure data: an ordered list of candidates. The panel walks
// it from largest to smallest and takes the first tier whose minimum size
// fits the window. Order lives here (unit-testable), not in view code.
namespace Ftop.Core;

public enum Tier { Xs, S, M, L }

public sealed record LadderCandidate(Tier Tier, double MinWidth, double MinHeight, int MaxProcessRows);

public static class LayoutLadder
{
    // Ordered largest-first. A candidate fits when the window is at least
    // its minimum size; the first fit wins, so nothing is ever clipped.
    public static readonly IReadOnlyList<LadderCandidate> Candidates = new[]
    {
        new LadderCandidate(Tier.L, 430, 470, 12),
        new LadderCandidate(Tier.M, 320, 330, 0),
        new LadderCandidate(Tier.S, 230, 240, 0),
        new LadderCandidate(Tier.Xs, 150, 150, 0),
    };

    public static (Tier Tier, int ProcessRows) Pick(double width, double height)
    {
        foreach (var c in Candidates)
        {
            if (width >= c.MinWidth && height >= c.MinHeight)
            {
                int rows = c.MaxProcessRows;
                if (c.Tier == Tier.L)
                {
                    // Grow the process list with the window instead of the type size.
                    rows = Math.Clamp((int)((height - 430) / 24), 3, c.MaxProcessRows);
                }
                return (c.Tier, rows);
            }
        }
        return (Tier.Xs, 0);
    }

    public static bool ShowsModule(Tier tier, string module) => module switch
    {
        "cores" => true,
        "memory" => tier >= Tier.S,
        "network" => tier >= Tier.S,
        "gpu" => tier >= Tier.M,
        "power" => tier >= Tier.M,
        "processes" => tier == Tier.L,
        _ => false,
    };
}
