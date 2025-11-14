import { z } from 'zod';

export const ROLE_SCHEMA = z.enum(['admin', 'user']);
export type Role = z.infer<typeof ROLE_SCHEMA>;
