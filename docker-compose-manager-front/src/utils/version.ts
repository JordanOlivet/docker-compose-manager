/**
 * Application version information
 * These values are injected at build time by Vite
 */

export const APP_VERSION = __APP_VERSION__
export const BUILD_DATE = __BUILD_DATE__
export const GIT_COMMIT = __GIT_COMMIT__

export interface VersionInfo {
  version: string
  buildDate: string
  gitCommit: string
}

export function getVersionInfo(): VersionInfo {
  return {
    version: APP_VERSION,
    buildDate: BUILD_DATE,
    gitCommit: GIT_COMMIT,
  }
}

export function getShortCommit(): string {
  return GIT_COMMIT.substring(0, 7)
}
