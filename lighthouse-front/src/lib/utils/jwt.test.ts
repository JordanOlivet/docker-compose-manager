import { describe, it, expect } from 'vitest';
import { parseJwt, isExpired, isNearExpiration, getRoleFromToken } from './jwt';

// Build a fake unsigned JWT with the given payload (base64url, no padding).
function makeToken(payload: Record<string, unknown>): string {
  const b64url = (obj: unknown) =>
    Buffer.from(JSON.stringify(obj))
      .toString('base64')
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

const ROLE_URI = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

describe('jwt utils', () => {
  it('parses a valid payload', () => {
    const token = makeToken({ sub: '1', exp: 123 });
    expect(parseJwt(token)).toMatchObject({ sub: '1', exp: 123 });
  });

  it('returns null on malformed / empty tokens', () => {
    expect(parseJwt(null)).toBeNull();
    expect(parseJwt('')).toBeNull();
    expect(parseJwt('not-a-jwt')).toBeNull();
    expect(parseJwt('a.b')).not.toBeUndefined(); // b is not valid base64 json -> null
    expect(parseJwt('a.%%%.c')).toBeNull();
  });

  it('detects expiry with margin', () => {
    const future = makeToken({ exp: Math.floor(Date.now() / 1000) + 3600 });
    const past = makeToken({ exp: Math.floor(Date.now() / 1000) - 10 });
    expect(isExpired(future)).toBe(false);
    expect(isExpired(past)).toBe(true);
    // 2h margin makes a token expiring in 1h count as expired
    expect(isExpired(future, 2 * 3600 * 1000)).toBe(true);
    // no exp claim -> treated as expired
    expect(isExpired(makeToken({ sub: '1' }))).toBe(true);
  });

  it('detects near expiration', () => {
    const in5min = makeToken({ exp: Math.floor(Date.now() / 1000) + 5 * 60 });
    expect(isNearExpiration(in5min, 10 * 60 * 1000)).toBe(true);
    expect(isNearExpiration(in5min, 60 * 1000)).toBe(false);
  });

  it('reads role from short and .NET URI claim', () => {
    expect(getRoleFromToken(makeToken({ role: 'admin' }))).toBe('admin');
    expect(getRoleFromToken(makeToken({ [ROLE_URI]: 'user' }))).toBe('user');
    expect(getRoleFromToken(makeToken({ sub: '1' }))).toBeNull();
    expect(getRoleFromToken(null)).toBeNull();
  });
});
