import type { EntityState } from "./global";

export interface Container {
  id: string;
  name: string;
  image: string;
  status: string;
  state: EntityState;
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