using GonzoOverlay.Infrastructure.iRacing;

var estimator = new LeaderLapEstimator();
var now = DateTimeOffset.UtcNow;
double? Step(int lap, double? last, double time, bool pit = false, int leader = 0, int session = 1, bool finished = false)
{
    var positions = leader == 0 ? new double?[] { 1, 2 } : new double?[] { 2, 1 };
    var laps = new double?[] { lap, lap };
    var times = new double?[] { last, last };
    var pits = new bool?[] { pit, pit };
    now = now.AddSeconds(1);
    return estimator.Update(session, 100, true, finished, positions, laps, times, pits, time, now);
}

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Check(Step(0, null, 600) is null, "initial state");
Check(Step(1, 90, 540) is null, "initial incomplete lap skipped");
Check(Step(2, 61, 480) is null, "one sample");
Check(Step(3, 59, 420) is null, "two samples");
var estimate = Step(4, 60, 360);
Check(estimate.HasValue && Math.Abs(estimate.Value - 10) < 0.0001, "three-lap average");
Check(Step(4, 60, 350) == estimate, "estimate stays fixed between crossings");
Step(4, 60, 340, pit: true);
Check(Math.Abs(Step(5, 120, 300)!.Value - 10) < 0.0001, "pit lap excluded");
Step(5, 120, 290, leader: 1);
Check(Math.Abs(Step(6, 58, 240, leader: 1)!.Value - (6 + 240.0 / 59)) < 0.0001, "new leader sample");
Check(Math.Abs(Step(7, 63, 180, leader: 1)!.Value - (7 + 180.0 / 59)) < 0.0001, "three fastest of five");
Check(Math.Abs(Step(8, 57, 120, leader: 1)!.Value - (8 + 120.0 / 58)) < 0.0001, "oldest sample removed");
var beforeFinish = Step(8, 57, 0, leader: 1);
Check(Step(9, 56, 624, leader: 1, finished: true) == beforeFinish, "finish freezes estimate");
Check(Step(10, 55, 620, leader: 1) == beforeFinish && estimator.IsFinished, "finish stays latched");
Check(Step(0, null, 600, session: 2) is null, "session reset");
Check(!estimator.IsFinished, "finish reset for new session");

var delayed = new LeaderLapEstimator();
double? Delayed(int lap, double? last, double time)
{
    now = now.AddSeconds(1);
    return delayed.Update(1, 100, true, false, new double?[] { 1 }, new double?[] { lap },
        new double?[] { last }, new bool?[] { false }, time, now);
}
Delayed(0, null, 600);
Delayed(1, 90, 540);
Delayed(2, 61, 480);
Delayed(3, 59, 420);
Check(Delayed(4, 59, 360) is null, "await delayed last lap");
Check(Math.Abs(Delayed(4, 60, 359)!.Value - 10) < 0.0001, "use time captured at crossing");
Console.WriteLine("Leader lap estimator smoke checks passed.");
