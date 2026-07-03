// Small helpers for reading the (unverified) client-side JWT access token. These never
// validate the signature — that is the server's job — they only read claims the client
// legitimately needs (expiry for proactive refresh, role for immediate UI gating).

export interface JwtPayload {
  exp?: number;
  [key: string]: unknown;
}

/** .NET emits the role claim under this URI unless claim mapping is customised. */
const ROLE_CLAIM_URI = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

/** Decode a JWT payload (base64url). Returns null on any malformed input. */
export function parseJwt(token: string | null | undefined): JwtPayload | null {
  if (!token) return null;
  try {
    const part = token.split('.')[1];
    if (!part) return null;
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    return JSON.parse(atob(padded)) as JwtPayload;
  } catch {
    return null;
  }
}

/** True if the token is expired (or unparseable), with an optional safety margin in ms. */
export function isExpired(token: string, marginMs = 0): boolean {
  const payload = parseJwt(token);
  if (!payload?.exp) return true;
  return payload.exp * 1000 < Date.now() + marginMs;
}

/** True if the token expires within `withinMs` (or is unparseable). */
export function isNearExpiration(token: string, withinMs: number): boolean {
  const payload = parseJwt(token);
  if (!payload?.exp) return true;
  return payload.exp * 1000 - Date.now() < withinMs;
}

/** Read the role claim from an access token, or null. */
export function getRoleFromToken(token: string | null | undefined): string | null {
  const payload = parseJwt(token);
  if (!payload) return null;
  const role = (payload['role'] ?? payload[ROLE_CLAIM_URI]) as string | undefined;
  return role ?? null;
}
