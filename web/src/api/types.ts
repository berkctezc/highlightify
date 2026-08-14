export type AppConfiguration = {
  spotifyConfigured: boolean
  ytDlpAvailable: boolean
  defaultBrowserSource: string
  version: string
}

export type SpotifyProfile = {
  id: string
  displayName: string
  imageUrl: string | null
  externalUrl: string | null
}

export type SpotifyConnection = {
  connected: boolean
  configured: boolean
  profile: SpotifyProfile | null
}

export type SpotifyPlaylist = {
  id: string
  name: string
  imageUrl: string | null
  trackCount: number
  isPublic: boolean
  externalUrl: string | null
}

export type SpotifyTrack = {
  id: string
  uri: string
  name: string
  artist: string
  album: string
  imageUrl: string | null
  externalUrl: string | null
  durationMs: number
  explicit: boolean
  matchScore: number
}

export type ImportStatus =
  | "queued"
  | "reading"
  | "matching"
  | "ready"
  | "exporting"
  | "completed"
  | "failed"

export type ImportTrack = {
  id: string
  title: string
  artist: string | null
  album: string | null
  source: string
  match: SpotifyTrack | null
  alternatives: SpotifyTrack[]
}

export type ImportJob = {
  id: string
  status: ImportStatus
  progress: number
  statusMessage: string
  createdAt: string
  updatedAt: string
  sources: string[]
  tracks: ImportTrack[]
  error: string | null
  playlistId: string | null
  playlistUrl: string | null
}

export type ExportPlaylistInput = {
  playlistId: string | null
  playlistName: string | null
  isPublic: boolean
  trackUris: string[]
}

export type StartImportInput = {
  sources: string[]
  files: File[]
  browserSource: string
}
