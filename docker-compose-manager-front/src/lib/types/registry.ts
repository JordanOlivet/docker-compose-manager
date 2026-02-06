/**
 * Request to login to a Docker registry
 */
export interface RegistryLoginRequest {
  registryUrl: string;
  authType: 'password' | 'token';
  username?: string;
  password?: string;
  token?: string;
}

/**
 * Request to logout from a Docker registry
 */
export interface RegistryLogoutRequest {
  registryUrl: string;
}

/**
 * Information about a configured Docker registry
 */
export interface ConfiguredRegistry {
  registryUrl: string;
  username?: string | null;
  isConfigured: boolean;
  usesCredentialHelper: boolean;
  credentialHelperName?: string | null;
}

/**
 * Status of a Docker registry including connection test result
 */
export interface RegistryStatus {
  registryUrl: string;
  isConfigured: boolean;
  isConnected: boolean;
  username?: string | null;
  error?: string | null;
}

/**
 * Result of a registry login operation
 */
export interface RegistryLoginResult {
  success: boolean;
  message?: string | null;
  error?: string | null;
}

/**
 * Result of a registry logout operation
 */
export interface RegistryLogoutResult {
  success: boolean;
  message?: string | null;
}

/**
 * Result of a registry connection test
 */
export interface RegistryTestResult {
  success: boolean;
  isAuthenticated: boolean;
  message?: string | null;
  error?: string | null;
}

/**
 * Known registry information for display purposes
 */
export interface KnownRegistryInfo {
  name: string;
  registryUrl: string;
  description: string;
  icon: string;
}
