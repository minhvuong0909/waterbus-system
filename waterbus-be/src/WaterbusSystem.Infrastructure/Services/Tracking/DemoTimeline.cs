namespace WaterbusSystem.Infrastructure.Services.Tracking;

/// <summary>
/// Dòng thời gian một chuyến demo ngắn: tàu đứng yên (dwell) tại mỗi bến rồi chạy sang bến kế tiếp với tốc độ tăng/giảm dần
/// (smoothstep) nên chậm lại khi gần bến; thời gian từng chặng tỉ lệ với độ dài chặng. Lặp vòng sau <c>totalSeconds</c>.
/// </summary>
public static class DemoTimeline
{
    /// <param name="stopFractions">Vị trí các bến trên đường đi (0..1, tăng dần, gồm bến đầu và cuối).</param>
    public static (double Fraction, bool Dwelling) At(
        IReadOnlyList<double> stopFractions, double totalSeconds, double dwellSeconds, double elapsedSeconds)
    {
        var n = stopFractions.Count;
        if (n < 2 || totalSeconds <= 0) return (n == 0 ? 0 : stopFractions[0], true);
        var span = stopFractions[n - 1] - stopFractions[0];
        if (span <= 0) return (stopFractions[0], true);

        // Luôn chừa thời gian để di chuyển: dwell tối đa một nửa mỗi "phần" của chuyến.
        var dwell = Math.Clamp(dwellSeconds, 0, totalSeconds / (2.0 * n));
        var moving = totalSeconds - n * dwell;

        var t = elapsedSeconds % totalSeconds;
        if (t < 0) t += totalSeconds;
        for (var i = 0; i < n; i++)
        {
            if (t < dwell) return (stopFractions[i], true);
            t -= dwell;
            if (i == n - 1) break;
            var leg = stopFractions[i + 1] - stopFractions[i];
            var legSeconds = moving * leg / span;
            if (t < legSeconds)
            {
                var u = legSeconds <= 0 ? 1 : t / legSeconds;
                var eased = u * u * (3 - 2 * u);
                return (stopFractions[i] + leg * eased, false);
            }
            t -= legSeconds;
        }
        return (stopFractions[n - 1], true);
    }
}
