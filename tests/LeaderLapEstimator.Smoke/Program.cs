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

var fuel = FuelStrategyCalculator.Calculate(true, 10, null, 5, 0.5, 2, 5.5f, 0x10, 6);
Check(Math.Abs(fuel.ToAdd!.Value - 5.5) < 0.001 && Math.Abs(fuel.AtFinish!.Value - 0.5) < 0.001,
    "lap race, fractional progress and selected fuel");
fuel = FuelStrategyCalculator.Calculate(true, 32767, 9.3, 5, 0.5, 2, 5.5f, 0, 6);
Check(Math.Abs(fuel.ToAdd!.Value - 5.5) < 0.001 && Math.Abs(fuel.AtFinish!.Value + 5.5) < 0.001,
    "timed race and disabled fuel service");
fuel = FuelStrategyCalculator.Calculate(true, 32767, null, 5, 0.5, 2, 5.5f, 0x10, 6);
Check(fuel.ToAdd is null && fuel.AtFinish is null, "missing timed-race estimate");
fuel = FuelStrategyCalculator.Calculate(true, 10, null, 5, 0.5, 2, 20, null, null);
Check(fuel.ToAdd == 0 && fuel.AtFinish is null, "missing pit strategy does not hide refill");
fuel = FuelStrategyCalculator.Calculate(false, 10, null, 5, 0.5, 2, 5.5f, 0x10, 6);
Check(fuel.ToAdd is null && fuel.AtFinish is null, "no strategy outside race");
Console.WriteLine("Fuel strategy smoke checks passed.");

var tracker = new FuelLapTracker();
void FuelTick(int lap, float level, bool pit = false, int session = 1, float planned = 6, int? total = 30, int? flags = 0x10)
{
    tracker.Update(session, 100, 0, lap, level, pit, true, total, null, flags, planned);
}

FuelTick(0, 40);
FuelTick(1, 38);
FuelTick(2, 36);
Check(tracker.AverageConsumption == 2, "first clean fuel sample");
Check(tracker.FuelAtFinishSnapshotLap == 2 && tracker.FuelToAddSnapshotLap == 2,
    "strategy snapshot lap recorded");
var beforePitAverage = tracker.AverageConsumption;
var toAddAtLine = tracker.FuelToAdd;
var atFinishAtLine = tracker.FuelAtFinish;
FuelTick(2, 35, planned: 6);
Check(tracker.FuelToAdd == toAddAtLine && tracker.FuelAtFinish == atFinishAtLine,
    "normal fuel consumption does not change strategy between crossings");
FuelTick(2, 34.5f, planned: 20);
Check(tracker.FuelToAdd == toAddAtLine && Math.Abs(tracker.FuelAtFinish!.Value - (atFinishAtLine!.Value + 14)) < 0.001,
    "changing planned fuel immediately updates finish estimate only");
FuelTick(2, 34.4f, planned: 20, flags: 0);
Check(Math.Abs(tracker.FuelAtFinish!.Value - (atFinishAtLine!.Value - 6)) < 0.001,
    "disabling fuel service immediately updates finish estimate");
FuelTick(2, 34, pit: true, planned: 6);
FuelTick(2, 34.01f, pit: true, planned: 6);
Check(Math.Abs(tracker.FuelToAdd!.Value - (toAddAtLine!.Value - 0.01f)) < 0.001,
    "refill updates needed fuel by hundredths");
FuelTick(2, 44, pit: true, planned: 6);
Check(Math.Abs(tracker.FuelToAdd!.Value - (toAddAtLine!.Value - 10)) < 0.001,
    "refill reduces needed fuel by actual added amount");
Check(tracker.AverageConsumption == beforePitAverage && tracker.LapsOfFuelRemaining.HasValue,
    "refuel keeps known averages and range");
FuelTick(3, 44, pit: true);
FuelTick(3, 43, pit: false);
FuelTick(4, 42);
Check(tracker.AverageConsumption == beforePitAverage && tracker.FuelToAdd.HasValue,
    "pit entry and exit laps do not erase average");
for (var lap = 5; lap <= 8; lap++)
{
    FuelTick(lap, 42 - (lap - 4) * 3);
    Check(tracker.AverageConsumption == beforePitAverage, "keep average while collecting five clean laps");
}
FuelTick(9, 27);
Check(tracker.AverageConsumption == 3, "replace average after five clean laps");
var knownRefill = tracker.FuelToAdd;
var knownFinish = tracker.FuelAtFinish;
FuelTick(10, 24, total: null);
Check(tracker.FuelToAdd == knownRefill && tracker.FuelAtFinish == knownFinish,
    "temporary unavailable race estimate keeps last valid strategy");
Check(tracker.FuelAtFinishSnapshotLap == 9 && tracker.FuelToAddSnapshotLap == 9,
    "cached strategy retains source lap when race estimate is unavailable");
FuelTick(0, 40, session: 2);
Check(tracker.AverageConsumption is null && tracker.FuelToAdd is null && tracker.FuelAtFinish is null,
    "fuel history does not leak between sessions");
Console.WriteLine("Fuel lap snapshot and pit recovery checks passed.");

var standingsGaps = new StandingsLapGap();
standingsGaps.BeginSession(1, 100, true);
Check(standingsGaps.UpdateDriver(2, 8, 8).Leader is null, "wait for driver crossing after startup");
Check(standingsGaps.UpdateDriver(2, 9, 9).Leader == 0, "driver on lead lap after crossing");
Check(standingsGaps.UpdateDriver(2, 9, 10).Leader == 0, "leader crossing does not update driver gap");
Check(standingsGaps.UpdateDriver(2, 10, 10).Leader == 0, "driver crossing after leader on same lap");
Check(standingsGaps.UpdateDriver(2, 10, 11).Leader == 0, "lapped mid-lap does not update gap");
Check(standingsGaps.UpdateDriver(2, 11, 12).Leader == 1, "first crossing one lap down");
Check(standingsGaps.UpdateDriver(2, 11, 13).Leader == 1, "gap stays fixed between crossings");
Check(standingsGaps.UpdateDriver(2, 12, 14).Leader == 2, "multiple laps behind at crossing");
Check(standingsGaps.UpdateDriver(2, null, 14).Leader == 2, "missing driver lap data keeps previous gap");
Check(standingsGaps.UpdateDriver(2, 12, null).Leader == 2, "missing leader lap data keeps previous gap");
Check(standingsGaps.UpdateDriver(2, 13, 13).Leader == 0, "gap decreases after unlapping");

standingsGaps.UpdateDriver(4, 13, 15, 3, 15);
var fourth = standingsGaps.UpdateDriver(4, 14, 15, 3, 14);
Check(fourth.Leader == 1 && fourth.Ahead == 0, "one lap from leader but same lap as car ahead");
standingsGaps.UpdateDriver(5, 12, 15, 4, 14);
var fifth = standingsGaps.UpdateDriver(5, 13, 15, 4, 14);
Check(fifth.Leader == 2 && fifth.Ahead == 1, "two laps from leader and one from car ahead");
standingsGaps.UpdateDriver(6, 12, 15, 5, 13);
var sixth = standingsGaps.UpdateDriver(6, 13, 15, 5, 13);
Check(sixth.Leader == 2 && sixth.Ahead == 0, "two laps from leader but same lap as car ahead");
Check(standingsGaps.UpdateDriver(5, 13, 16, 4, 15).Ahead == 1,
    "car ahead crossing does not update interval laps");
Check(standingsGaps.UpdateDriver(5, 13, 16, 3, 16).Ahead is null,
    "changed car ahead does not reuse another driver's interval");
Check(standingsGaps.UpdateDriver(5, 14, 16, 3, 16).Ahead == 2,
    "new car ahead measured on next crossing");
standingsGaps.BeginSession(2, 100, true);
Check(standingsGaps.UpdateDriver(2, 0, 0).Leader is null, "new session resets old gap");
standingsGaps.BeginSession(2, 100, false);
Check(standingsGaps.UpdateDriver(2, 1, 2) == new StandingsLapGaps(null, null), "no lap gap outside race");
Console.WriteLine("Standings lap gap smoke checks passed.");
