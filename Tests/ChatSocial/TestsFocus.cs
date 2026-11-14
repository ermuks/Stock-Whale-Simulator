using System;

public static partial class ChatSocialTests
{
    private static void FocusScenarios()
    {
        double minute = 0;
        var focus = new MarketConversationFocus { GameMinutes = () => minute };
        var small = new Stock { symbol = "SMALL", stockName = "소형", currentPrice = 1000 };
        var large = new Stock { symbol = "LARGE", stockName = "대형", currentPrice = 1000 };
        for (int t = 0; t < 180; t++)
        {
            minute = t;
            focus.Record(small, 1000, 10, t * 3); focus.Record(large, 1000, 1000, t * 3);
            focus.Read(small, t * 3);
        }
        var a = focus.Read(small, 179 * 3); var b = focus.Read(large, 179 * 3);
        Check(Math.Abs(a.Probability - b.Probability) < .000001, "A equal relative activity ignores 100x absolute volume difference");
        Check(Math.Abs(a.DwellMultiplier - .3) < .000001, "A continuous hot residence reaches configured 0.3 floor");
        for (int t = 180; t < 185; t++)
        {
            minute = t;
            focus.Record(small, 1100, 50, t * 3); focus.Record(large, 1000, 1000, t * 3);
        }
        a = focus.Read(small, 184 * 3); b = focus.Read(large, 184 * 3);
        Check(a.Probability > b.Probability && a.ReturnPercent == 10, "A own-volume surge and exact five-minute return raise attention");
        double unchanged = a.HeatScore;
        Check(focus.Read(small, 1000).HeatScore == unchanged, "A elapsed real seconds do not age a paused game-minute window");
        minute = 400;
        Check(!focus.HasActivity(1200) && focus.Read(small, 1200).HeatScore == 0 && Math.Abs(focus.Read(small, 1200).Probability - .5) < .000001, "A stale windows clear and empty market falls back to neutral finite weights");
        large.IsListed = false;
        Check(focus.Read(large, 1210).Probability == 0 && focus.Read(small, 1210).Probability == 1, "A delisted stock receives no attention");
    }
}
