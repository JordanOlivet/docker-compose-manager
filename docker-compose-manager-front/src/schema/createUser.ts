import { z } from 'zod';
import { ROLE_SCHEMA } from './role';

export const createUserSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  role: ROLE_SCHEMA,
});
export type CreateUserFormData = z.infer<typeof createUserSchema>;
