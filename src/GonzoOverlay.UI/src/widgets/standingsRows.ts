import type { StandingEntry } from '../overlay/types';

export function selectStandingsRows(entries: StandingEntry[], capacity: number): StandingEntry[] {
  const count = Math.max(0, Math.floor(capacity));
  const playerIndex = entries.findIndex((entry) => entry.isPlayer);
  if (playerIndex < 0 || playerIndex < count || entries.length <= count) {
    return entries.slice(0, count);
  }

  // In a very short widget, reserve at least one row for the player.
  const leadersCount = Math.min(3, Math.max(0, count - 1));
  const surroundingCount = count - leadersCount;
  const start = Math.max(
    leadersCount,
    Math.min(playerIndex - Math.floor(surroundingCount / 2), entries.length - surroundingCount),
  );
  return [...entries.slice(0, leadersCount), ...entries.slice(start, start + surroundingCount)];
}
