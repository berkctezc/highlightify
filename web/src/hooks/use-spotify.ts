import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { api } from "@/api/client"

export function useSpotifyConnection() {
  return useQuery({
    queryKey: ["spotify", "connection"],
    queryFn: ({ signal }) => api.getSpotifyConnection(signal),
    staleTime: 30_000,
    retry: 1,
  })
}

export function useSpotifyActions() {
  const queryClient = useQueryClient()
  const disconnect = useMutation({
    mutationFn: api.disconnectSpotify,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["spotify"] })
    },
  })

  return {
    disconnect,
  }
}
