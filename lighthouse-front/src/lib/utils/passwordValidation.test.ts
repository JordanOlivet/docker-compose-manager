import { describe, it, expect } from 'vitest';
import { validatePassword, PASSWORD_MIN_LENGTH } from './passwordValidation';

describe('validatePassword', () => {
  it('accepts a strong password', () => {
    const result = validatePassword('Str0ng!Pass');
    expect(result.isValid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('flags a password shorter than the minimum length', () => {
    const result = validatePassword('A1!a');
    expect(result.isValid).toBe(false);
    expect(result.errors.some((e) => e.key === 'auth.passwordRuleTooShort')).toBe(true);
    expect(result.errors[0].params).toEqual({ min: PASSWORD_MIN_LENGTH });
  });

  it('reports every failing rule for a weak password', () => {
    const result = validatePassword('password');
    const keys = result.errors.map((e) => e.key);
    expect(result.isValid).toBe(false);
    expect(keys).toEqual(
      expect.arrayContaining([
        'auth.passwordRuleUppercase',
        'auth.passwordRuleDigit',
        'auth.passwordRuleSpecial',
        'auth.passwordRuleWeak'
      ])
    );
  });
});
