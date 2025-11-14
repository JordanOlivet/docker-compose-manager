import { z } from 'zod';

export const composePathSchema = z.object({
  path: z.string().min(1, 'Path is required'),
  isReadOnly: z.boolean().default(false),
});
export type ComposePathFormData = z.infer<typeof composePathSchema>;
