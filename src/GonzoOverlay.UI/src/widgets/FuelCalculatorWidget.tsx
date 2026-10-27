import type { FuelSnapshot } from '../overlay/types';

type FuelCalculatorWidgetProps = {
  snapshot: FuelSnapshot;
};

function formatLiters(value: number | null) {
  return value === null ? '---' : `${value.toFixed(2)} L`;
}

function formatLaps(value: number | null) {
  return value === null ? '---' : value.toFixed(2);
}

function formatConsumption(value: number | null) {
  return value === null ? '---' : `${value.toFixed(2)} L/lap`;
}

export function FuelCalculatorWidget({ snapshot }: FuelCalculatorWidgetProps) {
  return (
    <section className="fuel-calculator-widget" aria-label="Fuel Calculator">
      <div className="fuel-calculator-current">
        <span className="fuel-calculator-label">AKTUALNE PALIWO</span>
        <strong>{formatLiters(snapshot.currentFuelLiters)}</strong>
      </div>
      <div className="fuel-calculator-grid">
        <div className="fuel-calculator-cell">
          <span className="fuel-calculator-label">ŚREDNIE ZUŻYCIE</span>
          <strong>{formatConsumption(snapshot.averageConsumptionPerLapLiters)}</strong>
        </div>
        <div className="fuel-calculator-cell">
          <span className="fuel-calculator-label">POZOSTAŁO</span>
          <strong>{formatLaps(snapshot.lapsOfFuelRemaining)}</strong>
        </div>
        <div className="fuel-calculator-cell">
          <span className="fuel-calculator-label">DOLEWKA</span>
          <strong>{formatLiters(snapshot.fuelToAddLiters)}</strong>
        </div>
      </div>
    </section>
  );
}
