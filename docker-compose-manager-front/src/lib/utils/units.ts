/**
 * Unit type with divisor
 */
export interface Unit {
  unit: string;
  divisor: number;
}

/**
 * Get the best unit for displaying data based on maximum value
 * @param maxValue Maximum value in the dataset
 * @param baseUnit Base unit (default: 'B' for bytes)
 * @returns Unit with divisor
 */
export function getBestUnit(maxValue: number, baseUnit: string = 'B'): Unit {
  const k = 1024;

  if (maxValue >= k * k * k) {
    return { unit: `G${baseUnit}`, divisor: k * k * k };
  }
  if (maxValue >= k * k) {
    return { unit: `M${baseUnit}`, divisor: k * k };
  }
  if (maxValue >= k) {
    return { unit: `K${baseUnit}`, divisor: k };
  }
  return { unit: baseUnit, divisor: 1 };
}

/**
 * Get the best unit for memory display
 * @param data Array of values
 * @param selector Function to extract value from data item
 * @returns Unit with divisor
 */
export function getBestMemoryUnit<T>(
  data: T[],
  selector: (item: T) => number
): Unit {
  if (data.length === 0) return { unit: 'KB', divisor: 1024 };
  const maxValue = Math.max(...data.map(selector));
  return getBestUnit(maxValue, 'B');
}

/**
 * Get the best unit for network display (cumulative bytes)
 * @param data Array of values
 * @param selector Function to extract value from data item
 * @returns Unit with divisor
 */
export function getBestNetworkUnit<T>(
  data: T[],
  selector: (item: T) => number
): Unit {
  if (data.length === 0) return { unit: 'KB', divisor: 1024 };
  const maxValue = Math.max(...data.map(selector));
  return getBestUnit(maxValue, 'B');
}

/**
 * Get the best unit for network rate display (bytes per second)
 * @param data Array of values
 * @param selector Function to extract value from data item
 * @returns Unit with divisor
 */
export function getBestNetworkRateUnit<T>(
  data: T[],
  selector: (item: T) => number
): Unit {
  if (data.length === 0) return { unit: 'KB/s', divisor: 1024 };
  const maxValue = Math.max(...data.map(selector));
  return getBestUnit(maxValue, 'B/s');
}

/**
 * Get the best unit for disk I/O display (cumulative bytes)
 * @param data Array of values
 * @param selector Function to extract value from data item
 * @returns Unit with divisor
 */
export function getBestDiskUnit<T>(
  data: T[],
  selector: (item: T) => number
): Unit {
  if (data.length === 0) return { unit: 'KB', divisor: 1024 };
  const maxValue = Math.max(...data.map(selector));
  return getBestUnit(maxValue, 'B');
}

/**
 * Get the best unit for disk I/O rate display (bytes per second)
 * @param data Array of values
 * @param selector Function to extract value from data item
 * @returns Unit with divisor
 */
export function getBestDiskRateUnit<T>(
  data: T[],
  selector: (item: T) => number
): Unit {
  if (data.length === 0) return { unit: 'KB/s', divisor: 1024 };
  const maxValue = Math.max(...data.map(selector));
  return getBestUnit(maxValue, 'B/s');
}

/**
 * Format bytes to human readable string
 * @param bytes Number of bytes
 * @param decimals Number of decimal places (default: 2)
 * @returns Formatted string
 */
export function formatBytes(bytes: number, decimals: number = 2): string {
  if (bytes === 0) return '0 B';

  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

/**
 * Format percentage value
 * @param value Percentage value (0-100)
 * @param decimals Number of decimal places (default: 1)
 * @returns Formatted string
 */
export function formatPercentage(value: number, decimals: number = 1): string {
  return `${value.toFixed(decimals)}%`;
}
