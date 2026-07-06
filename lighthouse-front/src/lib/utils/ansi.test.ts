import { describe, it, expect } from 'vitest';
import { parseAnsi, hasAnsi } from './ansi';

const ESC = '\x1b';

describe('parseAnsi', () => {
  it('returns a single plain segment for text without ANSI', () => {
    const segments = parseAnsi('hello world');
    expect(segments).toHaveLength(1);
    expect(segments[0]).toEqual({ text: 'hello world' });
  });

  it('applies a foreground color to following text', () => {
    const segments = parseAnsi(`${ESC}[31mred${ESC}[0m plain`);
    expect(segments[0]).toMatchObject({ text: 'red', fg: 'var(--ansi-red)' });
    expect(segments[segments.length - 1]).toMatchObject({ text: ' plain' });
    expect(segments[segments.length - 1].fg).toBeUndefined();
  });

  it('handles bold and multiple attributes', () => {
    const segments = parseAnsi(`${ESC}[1;32mbold green`);
    expect(segments[0]).toMatchObject({ text: 'bold green', bold: true, fg: 'var(--ansi-green)' });
  });

  it('resets styling with the bare [0m / [m form', () => {
    const segments = parseAnsi(`${ESC}[1mbold${ESC}[mplain`);
    expect(segments[0].bold).toBe(true);
    expect(segments[1]).toEqual({ text: 'plain' });
  });

  it('supports bright colors (90-97)', () => {
    const segments = parseAnsi(`${ESC}[91mbright red`);
    expect(segments[0].fg).toBe('var(--ansi-bright-red)');
  });

  it('supports background colors', () => {
    const segments = parseAnsi(`${ESC}[44mon blue`);
    expect(segments[0].bg).toBe('var(--ansi-blue)');
  });

  it('supports 256-color foreground', () => {
    const segments = parseAnsi(`${ESC}[38;5;196mx`);
    expect(segments[0].fg).toMatch(/^rgb\(/);
  });

  it('supports truecolor foreground', () => {
    const segments = parseAnsi(`${ESC}[38;2;10;20;30mx`);
    expect(segments[0].fg).toBe('rgb(10, 20, 30)');
  });

  it('strips non-SGR escape sequences without altering text', () => {
    const segments = parseAnsi(`a${ESC}[2Kb${ESC}[Hc`);
    expect(segments.map((s) => s.text).join('')).toBe('abc');
    expect(segments.every((s) => s.fg === undefined)).toBe(true);
  });

  it('handles an empty string', () => {
    expect(parseAnsi('')).toEqual([{ text: '' }]);
  });

  it('carries style across multiple segments until reset', () => {
    const segments = parseAnsi(`${ESC}[33mone${ESC}[1mtwo${ESC}[0mthree`);
    expect(segments[0]).toMatchObject({ text: 'one', fg: 'var(--ansi-yellow)' });
    expect(segments[1]).toMatchObject({ text: 'two', fg: 'var(--ansi-yellow)', bold: true });
    expect(segments[2]).toEqual({ text: 'three' });
  });
});

describe('hasAnsi', () => {
  it('detects escape sequences', () => {
    expect(hasAnsi(`${ESC}[31mx`)).toBe(true);
  });
  it('returns false for plain text', () => {
    expect(hasAnsi('plain text')).toBe(false);
  });
});
