/**
 * Logger utility that only logs in development mode
 */

const isDev = import.meta.env.MODE === 'development';

export const logger = {
  log: (...args: any[]) => {
    if (isDev) {
      console.log(...args);
    }
  },
  error: (...args: any[]) => {
    // if (isDev) {
      console.error(...args);
    // }
  },
  warn: (...args: any[]) => {
    // if (isDev) {
      console.warn(...args);
    // }
  },
  info: (...args: any[]) => {
    if (isDev) {
      console.info(...args);
    }
  },
  debug: (...args: any[]) => {
    if (isDev) {
      console.debug(...args);
    }
  },
};
