type DriverRatingBadgeProps = {
  safetyRating: string | null | undefined;
  iRating: number | null | undefined;
};

export function DriverRatingBadge({ safetyRating, iRating }: DriverRatingBadgeProps) {
  const license = safetyRating?.trim().charAt(0).toUpperCase() ?? '';
  const knownLicense = ['A', 'B', 'C', 'D', 'R'].includes(license);
  const rating = iRating != null && Number.isFinite(iRating) && iRating >= 0
    ? `${(Math.round(iRating / 100) / 10).toFixed(1)}K`
    : '--';

  return (
    <span className="driver-rating-badge" aria-label={`Licencja ${knownLicense ? license : 'brak'}, iRating ${rating}`}>
      <span className={`driver-rating-license driver-rating-license-${knownLicense ? license.toLowerCase() : 'unknown'}`}>
        {knownLicense ? license : '--'}
      </span>
      <span className="driver-rating-value">{rating}</span>
    </span>
  );
}
