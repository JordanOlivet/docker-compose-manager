import toast from 'react-hot-toast';

export const useToast = () => {
  const success = (message: string) => {
    toast.success(message, {
      duration: 3000,
      position: 'top-right',
    });
  };

  const error = (message: string) => {
    toast.error(message, {
      duration: 4000,
      position: 'top-right',
    });
  };

  const loading = (message: string) => {
    return toast.loading(message, {
      position: 'top-right',
    });
  };

  const dismiss = (toastId?: string) => {
    toast.dismiss(toastId);
  };

  return { success, error, loading, dismiss };
};
