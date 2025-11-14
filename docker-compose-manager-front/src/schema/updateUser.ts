import { z } from 'zod';
import { ROLE_SCHEMA } from './role';

export const updateUserSchema = z.object({
  role: ROLE_SCHEMA.optional(),
  isEnabled: z.boolean().optional(),
  newPassword: z.string().min(8, 'Password must be at least 8 characters').optional().or(z.literal('')),
});
export type UpdateUserFormData = z.infer<typeof updateUserSchema>;
