// Minimal ANSI SGR (Select Graphic Rendition) parser for log rendering.
//
// Deliberately dependency-free and produces structured segments (never HTML), so the
// viewer renders them as <span style=...> with no `@html` and therefore no XSS surface.
// Non-SGR escape sequences (cursor moves, clears, etc.) are stripped.

export interface AnsiStyle {
  fg?: string;
  bg?: string;
  bold?: boolean;
  dim?: boolean;
  italic?: boolean;
  underline?: boolean;
}

export interface AnsiSegment extends AnsiStyle {
  text: string;
}

// Standard 16-color palette as CSS custom properties, themed in the viewer's CSS.
// Index 0-7 normal, 8-15 bright.
const PALETTE_VARS = [
  '--ansi-black', '--ansi-red', '--ansi-green', '--ansi-yellow',
  '--ansi-blue', '--ansi-magenta', '--ansi-cyan', '--ansi-white',
  '--ansi-bright-black', '--ansi-bright-red', '--ansi-bright-green', '--ansi-bright-yellow',
  '--ansi-bright-blue', '--ansi-bright-magenta', '--ansi-bright-cyan', '--ansi-bright-white',
];

// eslint-disable-next-line no-control-regex
const ANSI_PATTERN = /\x1b\[([0-9;]*)m|\x1b\[[0-9;?]*[A-Za-z]|\x1b[()][0-9A-Za-z]|\x1b[A-Za-z]/g;

function paletteColor(index: number): string {
  return `var(${PALETTE_VARS[index]})`;
}

/** Maps an 8-bit (256) color index to a CSS color. */
function xterm256(n: number): string {
  if (n < 16) return paletteColor(n);
  if (n >= 232) {
    const gray = 8 + (n - 232) * 10;
    return `rgb(${gray}, ${gray}, ${gray})`;
  }
  const c = n - 16;
  const r = Math.floor(c / 36);
  const g = Math.floor((c % 36) / 6);
  const b = c % 6;
  const to = (v: number) => (v === 0 ? 0 : 55 + v * 40);
  return `rgb(${to(r)}, ${to(g)}, ${to(b)})`;
}

function applySgr(style: AnsiStyle, codes: number[]): AnsiStyle {
  const next: AnsiStyle = { ...style };
  for (let i = 0; i < codes.length; i++) {
    const code = codes[i];
    if (code === 0) {
      // reset all
      next.fg = next.bg = undefined;
      next.bold = next.dim = next.italic = next.underline = undefined;
    } else if (code === 1) next.bold = true;
    else if (code === 2) next.dim = true;
    else if (code === 3) next.italic = true;
    else if (code === 4) next.underline = true;
    else if (code === 22) next.bold = next.dim = undefined;
    else if (code === 23) next.italic = undefined;
    else if (code === 24) next.underline = undefined;
    else if (code >= 30 && code <= 37) next.fg = paletteColor(code - 30);
    else if (code >= 90 && code <= 97) next.fg = paletteColor(code - 90 + 8);
    else if (code >= 40 && code <= 47) next.bg = paletteColor(code - 40);
    else if (code >= 100 && code <= 107) next.bg = paletteColor(code - 100 + 8);
    else if (code === 39) next.fg = undefined;
    else if (code === 49) next.bg = undefined;
    else if (code === 38 || code === 48) {
      // extended color: 38;5;n (256) or 38;2;r;g;b (truecolor)
      const mode = codes[i + 1];
      let color: string | undefined;
      if (mode === 5) {
        color = xterm256(codes[i + 2]);
        i += 2;
      } else if (mode === 2) {
        color = `rgb(${codes[i + 2]}, ${codes[i + 3]}, ${codes[i + 4]})`;
        i += 4;
      }
      if (color) {
        if (code === 38) next.fg = color;
        else next.bg = color;
      }
    }
  }
  return next;
}

function hasStyle(style: AnsiStyle): boolean {
  return Boolean(style.fg || style.bg || style.bold || style.dim || style.italic || style.underline);
}

/**
 * Parses a log line into styled segments. When the line has no ANSI codes, returns a
 * single unstyled segment.
 */
export function parseAnsi(line: string): AnsiSegment[] {
  const segments: AnsiSegment[] = [];
  let style: AnsiStyle = {};
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  ANSI_PATTERN.lastIndex = 0;
  while ((match = ANSI_PATTERN.exec(line)) !== null) {
    if (match.index > lastIndex) {
      segments.push({ text: line.slice(lastIndex, match.index), ...style });
    }
    // Only SGR sequences (captured group present) change style; other escapes are stripped.
    if (match[1] !== undefined) {
      const codes = match[1] === '' ? [0] : match[1].split(';').map((c) => Number(c));
      style = applySgr(style, codes);
    }
    lastIndex = ANSI_PATTERN.lastIndex;
  }

  if (lastIndex < line.length) {
    segments.push({ text: line.slice(lastIndex), ...style });
  }

  if (segments.length === 0) {
    return [{ text: '' }];
  }
  return segments;
}

/** True when the line contains any ANSI escape sequence. */
export function hasAnsi(line: string): boolean {
  ANSI_PATTERN.lastIndex = 0;
  const found = ANSI_PATTERN.test(line);
  ANSI_PATTERN.lastIndex = 0;
  return found;
}

export { hasStyle as segmentHasStyle };
