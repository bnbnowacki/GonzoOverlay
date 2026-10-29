namespace GonzoOverlay.Infrastructure.iRacing;

// Race distance, consumption and baseline fuel are captured at the player's finish line.
internal sealed class FuelLapTracker
{
    private readonly Queue<float> _samples = new();
    private int? _sessionNumber;
    private int? _sessionId;
    private int? _playerCarIdx;
    private int? _lastCompletedLap;
    private float? _fuelAtLine;
    private float? _previousFuel;
    private bool _skipFirstSample = true;
    private bool _lapHadPit;
    private bool _wasOnPitRoad;
    private bool _rebuildingAfterPit;
    private int? _strategyCompletedLap;
    private int? _strategySessionLapsTotal;
    private double? _strategyEstimatedRaceLapsTotal;
    private float? _strategyAverageConsumption;
    private float? _strategyFuelAtLine;
    private float _fuelAddedSinceLine;

    public float? AverageConsumption { get; private set; }
    public float? LapsOfFuelRemaining { get; private set; }
    public float? FuelToAdd { get; private set; }
    public float? FuelAtFinish { get; private set; }
    public int? FuelToAddSnapshotLap { get; private set; }
    public int? FuelAtFinishSnapshotLap { get; private set; }
    public int CleanSampleCount => _samples.Count;
    public bool RebuildingAfterPit => _rebuildingAfterPit;
    public float? StrategyFuelAtLine => _strategyFuelAtLine;
    public float FuelAddedSinceLine => _fuelAddedSinceLine;

    public void Update(
        int? sessionNumber, int? sessionId, int? playerCarIdx,
        int? completedLap, float? fuel, bool? onPitRoad,
        bool isRace, int? sessionLapsTotal, double? estimatedRaceLapsTotal,
        int? pitServiceFlags, float? plannedFuel)
    {
        if (_sessionNumber != sessionNumber || _sessionId != sessionId || _playerCarIdx != playerCarIdx)
        {
            Reset();
            _sessionNumber = sessionNumber;
            _sessionId = sessionId;
            _playerCarIdx = playerCarIdx;
        }

        if (!completedLap.HasValue || completedLap.Value < 0 ||
            !fuel.HasValue || !float.IsFinite(fuel.Value) || fuel.Value < 0)
        {
            return;
        }

        if (_lastCompletedLap.HasValue && completedLap.Value < _lastCompletedLap.Value)
        {
            Reset();
            _sessionNumber = sessionNumber;
            _sessionId = sessionId;
            _playerCarIdx = playerCarIdx;
        }

        var isOnPitRoad = onPitRoad == true;
        if (isOnPitRoad && _wasOnPitRoad && _previousFuel.HasValue &&
            fuel.Value > _previousFuel.Value)
        {
            _fuelAddedSinceLine += fuel.Value - _previousFuel.Value;
        }
        var refuelDetected = _previousFuel.HasValue && fuel.Value > _previousFuel.Value + 0.001f;
        if ((isOnPitRoad && !_wasOnPitRoad) || refuelDetected)
        {
            _samples.Clear();
            _rebuildingAfterPit = AverageConsumption.HasValue;
            _lapHadPit = true;
        }

        _lapHadPit |= isOnPitRoad;
        _wasOnPitRoad = isOnPitRoad;
        _previousFuel = fuel;

        if (!_lastCompletedLap.HasValue || !_fuelAtLine.HasValue)
        {
            _lastCompletedLap = completedLap;
            _fuelAtLine = fuel;
            return;
        }

        if (completedLap.Value <= _lastCompletedLap.Value)
        {
            if (isRace)
            {
                RefreshStrategy(pitServiceFlags, plannedFuel);
            }
            return;
        }

        var consumption = _fuelAtLine.Value - fuel.Value;
        if (!_skipFirstSample && !_lapHadPit &&
            completedLap.Value == _lastCompletedLap.Value + 1 && consumption > 0.01f)
        {
            _samples.Enqueue(consumption);
            while (_samples.Count > 5)
            {
                _samples.Dequeue();
            }

            if (!_rebuildingAfterPit || _samples.Count == 5)
            {
                AverageConsumption = _samples.Average();
                _rebuildingAfterPit = false;
            }
        }

        _skipFirstSample = false;
        _lastCompletedLap = completedLap;
        _fuelAtLine = fuel;
        // If the line was crossed in the pits, the new lap is an out-lap too.
        _lapHadPit = isOnPitRoad;

        if (AverageConsumption is > 0)
        {
            LapsOfFuelRemaining = fuel.Value / AverageConsumption.Value;
        }

        var (toAdd, _) = FuelStrategyCalculator.Calculate(
            isRace, sessionLapsTotal, estimatedRaceLapsTotal,
            completedLap.Value + 1, 0, AverageConsumption, fuel,
            pitServiceFlags, plannedFuel);
        if (toAdd.HasValue)
        {
            _strategyCompletedLap = completedLap;
            _strategySessionLapsTotal = sessionLapsTotal;
            _strategyEstimatedRaceLapsTotal = estimatedRaceLapsTotal;
            _strategyAverageConsumption = AverageConsumption;
            _strategyFuelAtLine = fuel;
            _fuelAddedSinceLine = 0;
        }

        if (isRace)
        {
            RefreshStrategy(pitServiceFlags, plannedFuel);
        }
    }

    private void RefreshStrategy(int? pitServiceFlags, float? plannedFuel)
    {
        if (!_strategyCompletedLap.HasValue || !_strategyFuelAtLine.HasValue)
        {
            return;
        }

        var (toAdd, atFinish) = FuelStrategyCalculator.Calculate(
            true, _strategySessionLapsTotal, _strategyEstimatedRaceLapsTotal,
            _strategyCompletedLap.Value + 1, 0, _strategyAverageConsumption,
            _strategyFuelAtLine.Value + _fuelAddedSinceLine, pitServiceFlags, plannedFuel);
        if (toAdd.HasValue)
        {
            FuelToAdd = toAdd;
            FuelToAddSnapshotLap = _strategyCompletedLap;
        }
        if (atFinish.HasValue)
        {
            FuelAtFinish = atFinish;
            FuelAtFinishSnapshotLap = _strategyCompletedLap;
        }
    }

    public void Reset()
    {
        _samples.Clear();
        _sessionNumber = null;
        _sessionId = null;
        _playerCarIdx = null;
        _lastCompletedLap = null;
        _fuelAtLine = null;
        _previousFuel = null;
        _skipFirstSample = true;
        _lapHadPit = false;
        _wasOnPitRoad = false;
        _rebuildingAfterPit = false;
        _strategyCompletedLap = null;
        _strategySessionLapsTotal = null;
        _strategyEstimatedRaceLapsTotal = null;
        _strategyAverageConsumption = null;
        _strategyFuelAtLine = null;
        _fuelAddedSinceLine = 0;
        AverageConsumption = null;
        LapsOfFuelRemaining = null;
        FuelToAdd = null;
        FuelAtFinish = null;
        FuelToAddSnapshotLap = null;
        FuelAtFinishSnapshotLap = null;
    }
}
