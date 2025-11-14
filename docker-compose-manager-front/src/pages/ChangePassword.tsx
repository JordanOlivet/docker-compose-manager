import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api';
import { useToast } from '../hooks/useToast';
import { PasswordInput } from '../components/common/PasswordInput';
import { formatApiError, type ApiErrorResponse } from '../utils/errorFormatter';
import type { AxiosError } from 'axios';
import { t } from '../i18n';

function ChangePassword() {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const toast = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (newPassword !== confirmPassword) {
      toast.error(t('auth.passwordMismatch'));
      return;
    }

    if (newPassword.length < 8) {
      toast.error('Password must be at least 8 characters');
      return;
    }

    setLoading(true);
    try {
      await authApi.changePassword(currentPassword, newPassword);
      toast.success(t('auth.passwordChanged'));
      navigate('/dashboard');
    } catch (error: unknown) {
      toast.error(formatApiError(error as AxiosError<ApiErrorResponse>, 'Failed to change password'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-100">
      <div className="bg-white p-8 rounded-lg shadow-lg max-w-md w-full">
        <h1 className="text-2xl font-bold mb-6">{t('auth.changePassword')}</h1>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2">{t('auth.currentPassword')}</label>
            <PasswordInput
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className="w-full border rounded px-3 py-2 pr-10"
              placeholder={t('auth.currentPasswordPlaceholder')}
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">{t('auth.newPassword')}</label>
            <PasswordInput
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="w-full border rounded px-3 py-2 pr-10"
              placeholder={t('auth.newPasswordPlaceholder')}
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">{t('auth.confirmPassword')}</label>
            <PasswordInput
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className="w-full border rounded px-3 py-2 pr-10"
              placeholder={t('auth.confirmPasswordPlaceholder')}
              required
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-blue-500 text-white py-2 rounded hover:bg-blue-600 disabled:opacity-50"
          >
            {loading ? `${t('auth.changePassword')}...` : t('auth.changePassword')}
          </button>
        </form>
      </div>
    </div>
  );
}

export default ChangePassword;
