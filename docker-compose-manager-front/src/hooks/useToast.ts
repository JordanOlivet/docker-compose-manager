import toast from 'react-hot-toast';

export const useToast = () => {
  const success = (message: string) => {
    toast.success(message, {
      duration: 3000,
      position: 'top-right',
      style: {
        whiteSpace: 'pre-line', // Preserve line breaks
      },
    });
  };

  const error = (message: string) => {
    toast.error(message, {
      duration: 5000, // Increased duration for detailed error messages
      position: 'top-right',
      style: {
        whiteSpace: 'pre-line', // Preserve line breaks
        maxWidth: '500px', // Allow wider toasts for detailed messages
      },
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
