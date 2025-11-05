/**
 * Formats API error responses into user-friendly messages
 * Handles both simple error messages and detailed validation errors
 */

import { AxiosError } from 'axios';

export interface ApiErrorResponse {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Extracts and formats error message from API response
 * @param error - Axios error object
 * @param fallbackMessage - Default message if no error details are found
 * @returns Formatted error message
 */
export function formatApiError(error: AxiosError<ApiErrorResponse>, fallbackMessage: string = 'An error occurred'): string {
  // Extract error data from Axios response
  const errorData: ApiErrorResponse = error?.response?.data || {};

  // If there are detailed validation errors, format them
  if (errorData.errors && Object.keys(errorData.errors).length > 0) {
    const validationMessages: string[] = [];

    // Add main message if present
    if (errorData.message) {
      validationMessages.push(errorData.message);
    }

    // Add each field's errors
    for (const [, messages] of Object.entries(errorData.errors)) {
      messages.forEach(msg => {
        validationMessages.push(`• ${msg}`);
      });
    }

    return validationMessages.join('\n');
  }

  // Otherwise, return the simple message or fallback
  return errorData.message || fallbackMessage;
}

/**
 * Alternative formatter that returns an array of error messages
 * Useful for displaying multiple toast notifications
 */
export function formatApiErrorAsArray(error: AxiosError<ApiErrorResponse>): string[] {
  const errorData: ApiErrorResponse = error?.response?.data || {};
  const messages: string[] = [];

  if (errorData.errors && Object.keys(errorData.errors).length > 0) {
    // Add validation errors
    for (const [, fieldMessages] of Object.entries(errorData.errors)) {
      fieldMessages.forEach(msg => messages.push(msg));
    }
  } else if (errorData.message) {
    messages.push(errorData.message);
  }

  return messages;
}

/**
 * Checks if an error response contains validation errors
 */
export function hasValidationErrors(error: AxiosError<ApiErrorResponse>): boolean {
  const errorData: ApiErrorResponse = error?.response?.data || {};
  return !!(errorData.errors && Object.keys(errorData.errors).length > 0);
}
