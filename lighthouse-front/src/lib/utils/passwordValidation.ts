/**
 * Centralized password validation rules.
 * These rules mirror the backend PasswordRules (Validators/PasswordRules.cs)
 * to provide consistent client-side feedback before server validation.
 *
 * Returns i18n keys (auth.passwordRule*) with interpolation params so messages
 * can be translated and reference the actual configured values.
 */

export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

const WEAK_PATTERNS = ['12345', 'password', 'qwerty', 'abc123', 'admin'];

export interface PasswordValidationError {
	/** i18n key (e.g. "auth.passwordRuleTooShort") */
	key: string;
	/** interpolation params for i18next (e.g. { min: 8 }) */
	params?: Record<string, unknown>;
}

export interface PasswordValidationResult {
	isValid: boolean;
	errors: PasswordValidationError[];
}

/**
 * Validates a password against all complexity rules.
 * Returns all failing rules at once so the user can fix them in one go.
 */
export function validatePassword(password: string): PasswordValidationResult {
	const errors: PasswordValidationError[] = [];

	if (password.length < PASSWORD_MIN_LENGTH) {
		errors.push({ key: 'auth.passwordRuleTooShort', params: { min: PASSWORD_MIN_LENGTH } });
	}

	if (password.length > PASSWORD_MAX_LENGTH) {
		errors.push({ key: 'auth.passwordRuleTooLong', params: { max: PASSWORD_MAX_LENGTH } });
	}

	if (!/[A-Z]/.test(password)) {
		errors.push({ key: 'auth.passwordRuleUppercase' });
	}

	if (!/[a-z]/.test(password)) {
		errors.push({ key: 'auth.passwordRuleLowercase' });
	}

	if (!/[0-9]/.test(password)) {
		errors.push({ key: 'auth.passwordRuleDigit' });
	}

	if (!/[^a-zA-Z0-9]/.test(password)) {
		errors.push({ key: 'auth.passwordRuleSpecial' });
	}

	const lowerPassword = password.toLowerCase();
	if (WEAK_PATTERNS.some((pattern) => lowerPassword.includes(pattern))) {
		errors.push({ key: 'auth.passwordRuleWeak' });
	}

	return {
		isValid: errors.length === 0,
		errors
	};
}
