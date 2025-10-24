import { z } from 'zod';

/**
 * Login form schema
 */
export const loginSchema = z.object({
  username: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
});

export type LoginFormData = z.infer<typeof loginSchema>;

/**
 * Change password schema
 */
export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, 'Current password is required'),
  newPassword: z.string().min(8, 'Password must be at least 8 characters'),
  confirmPassword: z.string().min(1, 'Please confirm your password'),
}).refine((data) => data.newPassword === data.confirmPassword, {
  message: "Passwords don't match",
  path: ['confirmPassword'],
});

export type ChangePasswordFormData = z.infer<typeof changePasswordSchema>;

/**
 * Create user schema
 */
export const createUserSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  role: z.enum(['admin', 'user']),
});

export type CreateUserFormData = z.infer<typeof createUserSchema>;

/**
 * Update user schema
 */
export const updateUserSchema = z.object({
  role: z.enum(['admin', 'user']).optional(),
  isEnabled: z.boolean().optional(),
  newPassword: z.string().min(8, 'Password must be at least 8 characters').optional().or(z.literal('')),
});

export type UpdateUserFormData = z.infer<typeof updateUserSchema>;

/**
 * Compose file path schema
 */
export const composePathSchema = z.object({
  path: z.string().min(1, 'Path is required'),
  isReadOnly: z.boolean().default(false),
});

export type ComposePathFormData = z.infer<typeof composePathSchema>;
