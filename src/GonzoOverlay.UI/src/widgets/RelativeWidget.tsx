import type { RelativeSnapshot } from '../overlay/types';

type RelativeWidgetProps = {
  snapshot: RelativeSnapshot;
};

function formatGap(seconds: number | null, isPlayer: boolean) {
  if (isPlayer) {
    return '—';
  }

  if (seconds === null || !Number.isFinite(seconds)) {
    return '--';
  }

  return `${seconds > 0 ? '+' : ''}${seconds.toFixed(1)}`;
}

function formatPosition(position: number, isPlayer: boolean) {
  if (isPlayer) {
    return position > 0 ? position : 'YOU';
  }

  return position > 0 ? position : '--';
}

export function RelativeWidget({ snapshot }: RelativeWidgetProps) {
  const playerIndex = snapshot.entries.findIndex((entry) => entry.isPlayer);
  const ahead = playerIndex >= 0 ? snapshot.entries.slice(0, playerIndex) : [];
  const behind = playerIndex >= 0 ? snapshot.entries.slice(playerIndex + 1) : [];
  const rows = [
    ahead[2] ?? null,
    ahead[1] ?? null,
    ahead[0] ?? null,
    playerIndex >= 0 ? snapshot.entries[playerIndex] : null,
    behind[0] ?? null,
    behind[1] ?? null,
    behind[2] ?? null,
  ];

  return (
    <section className="relative-widget">
      <table className="relative-table">
        <tbody>
          {rows.map((entry, index) => {
            if (!entry) {
              return (
                <tr className="relative-empty" key={`empty-${index}`}>
                  <td />
                  <td />
                  <td />
                </tr>
              );
            }

            return (
              <tr
                key={`${entry.position}-${entry.driver}`}
                className={[
                  entry.isPlayer ? 'relative-player' : '',
                  entry.isOnPitRoad ? 'relative-pit' : '',
                ].filter(Boolean).join(' ') || undefined}
              >
                <td className="relative-position">
                  {formatPosition(entry.position, entry.isPlayer)}
                </td>
                <td className="relative-driver">{entry.driver}</td>
                <td className="relative-gap">
                  {formatGap(entry.gapToPlayerSeconds, entry.isPlayer)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}
