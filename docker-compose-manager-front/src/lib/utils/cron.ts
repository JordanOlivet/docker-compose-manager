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
 */
export function validateCron(expression: string, locale: string = 'en'): CronValidationResult {
  const trimmed = expression?.trim() ?? '';
  if (!trimmed) {
    return { valid: false, error: 'empty' };
  }

  try {
    const interval = CronExpressionParser.parse(trimmed);
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
