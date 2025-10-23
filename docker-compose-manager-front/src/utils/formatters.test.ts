import { describe, it, expect } from 'vitest';
import { formatBytes, formatRelativeTime, formatDate, formatCpuPercent, formatMemoryPercent } from './formatters';

describe('formatters', () => {
  describe('formatBytes', () => {
    it('formats 0 bytes correctly', () => {
      expect(formatBytes(0)).toBe('0 Bytes');
    });

    it('formats bytes correctly', () => {
      expect(formatBytes(1024)).toBe('1 KB');
      expect(formatBytes(1024 * 1024)).toBe('1 MB');
      expect(formatBytes(1024 * 1024 * 1024)).toBe('1 GB');
    });

    it('formats with decimals', () => {
      expect(formatBytes(1536, 2)).toBe('1.5 KB');
      expect(formatBytes(1536000, 2)).toBe('1.46 MB');
    });
  });

  describe('formatRelativeTime', () => {
    it('returns "just now" for recent dates', () => {
      const now = new Date();
      expect(formatRelativeTime(now)).toBe('just now');
    });

    it('formats minutes correctly', () => {
      const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
      expect(formatRelativeTime(fiveMinutesAgo)).toBe('5 minutes ago');
    });

    it('formats hours correctly', () => {
      const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000);
      expect(formatRelativeTime(twoHoursAgo)).toBe('2 hours ago');
    });

    it('formats days correctly', () => {
      const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000);
      expect(formatRelativeTime(threeDaysAgo)).toBe('3 days ago');
    });
  });

  describe('formatCpuPercent', () => {
    it('formats CPU percentage with 2 decimals', () => {
      expect(formatCpuPercent(45.678)).toBe('45.68%');
      expect(formatCpuPercent(100)).toBe('100.00%');
    });
  });

  describe('formatMemoryPercent', () => {
    it('formats memory percentage with 1 decimal', () => {
      expect(formatMemoryPercent(67.456)).toBe('67.5%');
      expect(formatMemoryPercent(100)).toBe('100.0%');
    });
  });

  describe('formatDate', () => {
    it('formats date to locale string', () => {
      const date = new Date('2024-01-15T10:30:00Z');
      const formatted = formatDate(date);
      expect(formatted).toContain('2024');
    });
  });
});
