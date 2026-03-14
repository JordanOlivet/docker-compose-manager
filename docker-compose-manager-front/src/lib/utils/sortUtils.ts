export type SortDir = 'asc' | 'desc';

/**
 * Tri alphabétique standard
 */
export function compareAlphabetic(a: string | null | undefined, b: string | null | undefined, dir: SortDir): number {
  const va = (a ?? '').toLowerCase();
  const vb = (b ?? '').toLowerCase();
  if (va < vb) return dir === 'asc' ? -1 : 1;
  if (va > vb) return dir === 'asc' ? 1 : -1;
  return 0;
}

/**
 * Tri numérique
 */
export function compareNumeric(a: number | null | undefined, b: number | null | undefined, dir: SortDir, nullsLast = true): number {
  const aNull = a == null;
  const bNull = b == null;
  if (aNull && bNull) return 0;
  if (aNull) return nullsLast ? 1 : -1;
  if (bNull) return nullsLast ? -1 : 1;
  return dir === 'asc' ? a - b : b - a;
}

/**
 * Tri par adresse IP (octet par octet)
 * Ex: 192.168.1.2 < 192.168.1.10
 * Les IPs vides/null vont à la fin
 */
export function compareIpAddress(a: string | null | undefined, b: string | null | undefined, dir: SortDir): number {
  const aEmpty = !a || a === '-';
  const bEmpty = !b || b === '-';
  if (aEmpty && bEmpty) return 0;
  if (aEmpty) return 1; // Vides à la fin
  if (bEmpty) return -1;

  const parseIp = (ip: string): number[] => {
    return ip.split('.').map(octet => parseInt(octet, 10) || 0);
  };

  const octetsA = parseIp(a);
  const octetsB = parseIp(b);

  for (let i = 0; i < 4; i++) {
    const diff = (octetsA[i] ?? 0) - (octetsB[i] ?? 0);
    if (diff !== 0) return dir === 'asc' ? diff : -diff;
  }
  return 0;
}

/**
 * Extrait le premier port hôte d'une chaîne de ports
 * Formats supportés: "8080", "8080:80", "0.0.0.0:8080->80/tcp"
 */
export function extractHostPort(portStr: string): number | null {
  if (!portStr || portStr === '-') return null;
  // Format "0.0.0.0:8080->80/tcp" ou "8080->80/tcp"
  const arrowMatch = portStr.match(/(?:[\d.]+:)?(\d+)->/);
  if (arrowMatch) return parseInt(arrowMatch[1], 10);
  // Format "8080:80"
  const colonMatch = portStr.match(/^(\d+):/);
  if (colonMatch) return parseInt(colonMatch[1], 10);
  // Format "8080"
  const simpleMatch = portStr.match(/^(\d+)/);
  if (simpleMatch) return parseInt(simpleMatch[1], 10);
  return null;
}

/**
 * Tri par ports (par premier port hôte)
 * Les containers sans ports vont à la fin
 */
export function comparePorts(a: string[] | null | undefined, b: string[] | null | undefined, dir: SortDir): number {
  const aEmpty = !a || a.length === 0;
  const bEmpty = !b || b.length === 0;
  if (aEmpty && bEmpty) return 0;
  if (aEmpty) return 1; // Sans ports à la fin
  if (bEmpty) return -1;

  // Prendre le plus petit port hôte de chaque liste
  const getMinPort = (ports: string[]): number => {
    const parsed = ports.map(extractHostPort).filter((p): p is number => p !== null);
    return parsed.length > 0 ? Math.min(...parsed) : Infinity;
  };

  const portA = getMinPort(a);
  const portB = getMinPort(b);

  if (portA === Infinity && portB === Infinity) return 0;
  if (portA === Infinity) return 1;
  if (portB === Infinity) return -1;

  return dir === 'asc' ? portA - portB : portB - portA;
}

/**
 * Tri par état avec priorité
 */
export function compareState(a: string, b: string, dir: SortDir, statePriority: Record<string, number>): number {
  const va = statePriority[a] ?? 99;
  const vb = statePriority[b] ?? 99;
  if (va === vb) return 0;
  return dir === 'asc' ? va - vb : vb - va;
}
