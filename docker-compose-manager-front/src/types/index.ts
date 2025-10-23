export interface User {
  id: number;
  username: string;
  role: string;
  isEnabled: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  username: string;
  role: string;
  mustChangePassword: boolean;
}

export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  message?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
}

export interface Container {
  id: string;
  name: string;
  image: string;
  status: string;
  state: string;
  created: string;
  labels?: Record<string, string>;
}

export interface ContainerDetails extends Container {
  env?: Record<string, string>;
  mounts?: Mount[];
  networks?: string[];
  ports?: Record<string, string>;
}

export interface Mount {
  type: string;
  source: string;
  destination: string;
  readOnly: boolean;
}
