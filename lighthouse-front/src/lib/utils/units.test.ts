import { describe, it, expect } from 'vitest';
import { getBestUnit, formatBytes, formatPercentage } from './units';

describe('getBestUnit', () => {
  it('selects the largest fitting unit', () => {
    expect(getBestUnit(500)).toEqual({ unit: 'B', divisor: 1 });
    expect(getBestUnit(2048)).toEqual({ unit: 'KB', divisor: 1024 });
    expect(getBestUnit(5 * 1024 * 1024)).toEqual({ unit: 'MB', divisor: 1024 * 1024 });
  });

  it('honours a custom base unit', () => {
    expect(getBestUnit(2048, 'B/s')).toEqual({ unit: 'KB/s', divisor: 1024 });
  });
});

describe('formatBytes', () => {
  it('formats zero', () => {
    expect(formatBytes(0)).toBe('0 B');
  });

  it('formats with the default 2 decimals', () => {
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(1024 * 1024)).toBe('1 MB');
  });
});

describe('formatPercentage', () => {
  it('formats with one decimal by default', () => {
    expect(formatPercentage(42)).toBe('42.0%');
    expect(formatPercentage(99.95, 0)).toBe('100%');
  });
});
