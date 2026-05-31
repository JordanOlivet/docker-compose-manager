import { CronExpressionParser } from 'cron-parser';
import cronstrue from 'cronstrue';

export interface CronValidationResult {
  valid: boolean;
  humanReadable?: string;
  nextRun?: Date;
  error?: string;
}

/**
 * Validates a cron expression and returns next-run plus human-readable description.
 *
 * The backend evaluates the cron in UTC (GetNextOccurrence(DateTime.UtcNow)), so we
 * parse in UTC too. The returned nextRun is an absolute instant; display it in the
 * browser's local timezone so the user sees the real wall-clock time it will fire.
 */
export function validateCron(expression: string, locale: string = 'en'): CronValidationResult {
  const trimmed = expression?.trim() ?? '';
  if (!trimmed) {
    return { valid: false, error: 'empty' };
  }

  try {
    const interval = CronExpressionParser.parse(trimmed, { tz: 'UTC' });
    const nextRun = interval.next().toDate();
    let humanReadable: string | undefined;
    try {
      humanReadable = cronstrue.toString(trimmed, { locale });
    } catch {
      humanReadable = undefined;
    }
    return { valid: true, humanReadable, nextRun };
  } catch (error) {
    return {
      valid: false,
      error: error instanceof Error ? error.message : 'invalid'
    };
  }
}

export function formatNextRun(date: Date | undefined): string {
  if (!date) return '';
  const day = date.getDate().toString().padStart(2, '0');
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const year = date.getFullYear();
  const hours = date.getHours().toString().padStart(2, '0');
  const minutes = date.getMinutes().toString().padStart(2, '0');
  return `${day}/${month}/${year} ${hours}:${minutes}`;
}

export interface CountdownLabels {
  day: string;
  hour: string;
  minute: string;
  soon: string;
}

/**
 * Returns the time remaining until `date` as a compact string (e.g. "2j 3h 5min").
 * Pass `now` so callers can drive a live ticking countdown from reactive state.
 */
export function formatCountdown(
  date: Date | undefined,
  labels: CountdownLabels,
  now: Date = new Date()
): string {
  if (!date) return '';
  const diffMs = date.getTime() - now.getTime();
  if (diffMs <= 0) return labels.soon;

  const totalMinutes = Math.floor(diffMs / 60000);
  const days = Math.floor(totalMinutes / 1440);
  const hours = Math.floor((totalMinutes % 1440) / 60);
  const minutes = totalMinutes % 60;

  const parts: string[] = [];
  if (days > 0) parts.push(`${days}${labels.day}`);
  if (hours > 0) parts.push(`${hours}${labels.hour}`);
  if (minutes > 0 || parts.length === 0) parts.push(`${minutes}${labels.minute}`);
  return parts.join(' ');
}
