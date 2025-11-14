import { useQuery, type UseQueryOptions, type QueryKey } from '@tanstack/react-query';

// Hook générique pour requêtes API avec options par défaut
export function useApiQuery<TQueryFnData, TError = unknown>(
  queryKey: QueryKey,
  queryFn: () => Promise<TQueryFnData>,
  options?: Omit<UseQueryOptions<TQueryFnData, TError, TQueryFnData, QueryKey>, 'queryKey' | 'queryFn'>
) {
  return useQuery<TQueryFnData, TError, TQueryFnData, QueryKey>({
    queryKey,
    queryFn,
    retry: 1,
    refetchOnWindowFocus: false,
    ...options,
  });
}
