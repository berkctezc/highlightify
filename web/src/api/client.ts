import type {
  AppConfiguration,
  ExportPlaylistInput,
  ImportJob,
  SpotifyConnection,
  SpotifyPlaylist,
  StartImportInput,
} from "@/api/types"

type ApiErrorPayload = { error?: string }

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message)
    this.name = "ApiError"
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: "include",
    ...init,
    headers: {
      ...(init?.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    let payload: ApiErrorPayload | null = null
    try {
      payload = (await response.json()) as ApiErrorPayload
    } catch {
      // A plain-text proxy or infrastructure error is shown with a useful fallback below.
    }
    throw new ApiError(payload?.error ?? `Request could not be completed (${response.status}).`, response.status)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  getConfiguration: (signal?: AbortSignal) =>
    request<AppConfiguration>("/api/config", { signal }),

  getSpotifyConnection: (signal?: AbortSignal) =>
    request<SpotifyConnection>("/api/auth/spotify/status", { signal }),

  disconnectSpotify: () =>
    request<void>("/api/auth/spotify/disconnect", { method: "POST" }),

  getPlaylists: (signal?: AbortSignal) =>
    request<SpotifyPlaylist[]>("/api/spotify/playlists", { signal }),

  getImports: (signal?: AbortSignal) =>
    request<ImportJob[]>("/api/imports", { signal }),

  getImport: (id: string, signal?: AbortSignal) =>
    request<ImportJob>(`/api/imports/${id}`, { signal }),

  startImport: async (input: StartImportInput) => {
    const form = new FormData()
    input.sources.forEach((source) => form.append("sources", source))
    input.files.forEach((file) => form.append("files", file))
    form.append("browserSource", input.browserSource)
    return request<{ id: string }>("/api/imports", { method: "POST", body: form })
  },

  retryMatching: (id: string) =>
    request<{ id: string }>(`/api/imports/${id}/match`, { method: "POST" }),

  exportPlaylist: (id: string, input: ExportPlaylistInput) =>
    request<ImportJob>(`/api/imports/${id}/export`, {
      method: "POST",
      body: JSON.stringify(input),
    }),
}

export function spotifyLoginUrl(returnPath = "/") {
  return `/api/auth/spotify/login?returnUrl=${encodeURIComponent(returnPath)}`
}
