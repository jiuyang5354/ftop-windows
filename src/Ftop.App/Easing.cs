namespace Ftop.App;

// Framerate-independent exponential easing toward live targets.
// Views call Ease(key, target, dt) every render pass; the value glides
// instead of jumping once a second — ftop's "columns follow the readings".
public sealed class EasingEngine
{
    private readonly Dictionary<string, double> _current = new();
    public bool ReducedMotion { get; set; }

    public double Ease(string key, double target, double dt)
    {
        if (ReducedMotion || dt <= 0)
        {
            _current[key] = target;
            return target;
        }
        if (!_current.TryGetValue(key, out double cur))
        {
            // First sighting: start at the target so the panel
            // does not sweep in from zero on launch.
            _current[key] = target;
            return target;
        }
        double next = cur + (target - cur) * (1.0 - Math.Exp(-dt * Theme.EaseSpeed));
        if (Math.Abs(next - target) < 0.0005) next = target;
        _current[key] = next;
        return next;
    }

    public void Snap(string key, double target) => _current[key] = target;

    public void Forget(string key) => _current.Remove(key);

    public void ForgetWhere(Func<string, bool> predicate)
    {
        foreach (var k in _current.Keys.Where(predicate).ToList())
            _current.Remove(k);
    }
}
