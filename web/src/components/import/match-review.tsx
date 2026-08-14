import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { AnimatePresence, motion } from "motion/react"
import { ArrowClockwiseIcon, CaretDownIcon, CheckCircleIcon, CheckIcon, CircleIcon, MusicNotesIcon, PlaylistIcon, ShieldCheckIcon } from "@/components/icons"
import { useState } from "react"
import { toast } from "sonner"

import { api } from "@/api/client"
import type { ImportJob, ImportTrack, SpotifyTrack } from "@/api/types"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { formatDuration, cn } from "@/lib/utils"

type TrackChoice = { enabled: boolean; uri: string | null }

export function MatchReview({ job }: { job: ImportJob }) {
  const queryClient = useQueryClient()
  const [expandedTrack, setExpandedTrack] = useState<string | null>(null)
  const [playlistMode, setPlaylistMode] = useState("new")
  const [playlistName, setPlaylistName] = useState("Instagram Highlights")
  const [isPublic, setIsPublic] = useState(false)
  const [choices, setChoices] = useState<Record<string, TrackChoice>>(() =>
    Object.fromEntries(job.tracks.map((track) => [track.id, { enabled: track.match !== null, uri: track.match?.uri ?? null }])),
  )
  const playlists = useQuery({
    queryKey: ["spotify", "playlists"],
    queryFn: ({ signal }) => api.getPlaylists(signal),
  })
  const exportPlaylist = useMutation({
    mutationFn: () => api.exportPlaylist(job.id, {
      playlistId: playlistMode === "new" ? null : playlistMode,
      playlistName: playlistMode === "new" ? playlistName : null,
      isPublic,
      trackUris: selectedUris,
    }),
    onSuccess: async () => {
      toast.success("Playlist hazır — Spotify'da bulabilirsin.")
      await queryClient.invalidateQueries({ queryKey: ["imports"] })
      await queryClient.invalidateQueries({ queryKey: ["import", job.id] })
      await queryClient.invalidateQueries({ queryKey: ["spotify", "playlists"] })
    },
    onError: (error: Error) => toast.error(error.message),
  })

  const selectedUris = Object.values(choices)
    .filter((choice) => choice.enabled && choice.uri)
    .map((choice) => choice.uri!)
  const matchedCount = job.tracks.filter((track) => track.match !== null).length
  const allSelectable = job.tracks.filter((track) => track.alternatives.length > 0)
  const allSelected = allSelectable.length > 0 && allSelectable.every((track) => choices[track.id]?.enabled)

  function toggleAll() {
    setChoices((current) => Object.fromEntries(job.tracks.map((track) => [
      track.id,
      track.alternatives.length > 0
        ? { enabled: !allSelected, uri: current[track.id]?.uri ?? track.match?.uri ?? track.alternatives[0]?.uri ?? null }
        : { enabled: false, uri: null },
    ])))
  }

  function toggleTrack(track: ImportTrack) {
    setChoices((current) => ({
      ...current,
      [track.id]: {
        enabled: !current[track.id]?.enabled,
        uri: current[track.id]?.uri ?? track.match?.uri ?? track.alternatives[0]?.uri ?? null,
      },
    }))
  }

  function chooseAlternative(track: ImportTrack, spotifyTrack: SpotifyTrack) {
    setChoices((current) => ({ ...current, [track.id]: { enabled: true, uri: spotifyTrack.uri } }))
    setExpandedTrack(null)
  }

  return (
    <div>
      <div className="mb-7 flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <Badge variant="success"><CheckCircleIcon className="size-3" weight="fill" /> Eşleştirme tamamlandı</Badge>
          <h2 className="type-page-title mt-4">Son kontrol sende.</h2>
          <p className="type-body mt-3">{matchedCount} eşleşme bulundu. İstediğin parçayı değiştirebilir veya aktarım dışında bırakabilirsin.</p>
        </div>
        <Button variant="ghost" size="sm" onClick={() => toggleAll()}>
          {allSelected ? <CircleIcon /> : <CheckCircleIcon weight="fill" />}{allSelected ? "Tümünü kaldır" : "Tümünü seç"}
        </Button>
      </div>

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_350px]">
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="flex items-center justify-between border-b border-white/7 px-4 py-3 text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground sm:hidden">
              <span>Spotify eşleşmeleri</span>
              <img src="/spotify-full-logo-white.svg" alt="Spotify" className="h-auto w-[70px] opacity-60" />
            </div>
            <div className="hidden grid-cols-[42px_minmax(0,1.1fr)_minmax(0,1fr)_72px_36px] gap-4 border-b border-white/7 px-5 py-3 text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground sm:grid">
              <span>#</span><span>Highlight müziği</span><span className="flex items-center gap-2">Spotify eşleşmesi <img src="/spotify-full-logo-white.svg" alt="Spotify" className="h-auto w-[70px] opacity-60" /></span><span>Süre</span><span />
            </div>
            <div className="divide-y divide-white/6">
              {job.tracks.map((track) => {
                const choice = choices[track.id]
                const chosen = track.alternatives.find((item) => item.uri === choice?.uri) ?? track.match
                const expanded = expandedTrack === track.id
                return (
                  <div key={track.id} className={cn("transition", choice?.enabled && "bg-white/[0.018]") }>
                    <div className="grid grid-cols-[36px_minmax(0,1fr)_32px] items-center gap-3 px-4 py-4 sm:grid-cols-[42px_minmax(0,1.1fr)_minmax(0,1fr)_72px_36px] sm:gap-4 sm:px-5">
                      <button
                        type="button"
                        role="checkbox"
                        aria-checked={Boolean(choice?.enabled)}
                        aria-label={`${track.title} parçasını ${choice?.enabled ? "çıkar" : "seç"}`}
                        disabled={track.alternatives.length === 0}
                        onClick={() => toggleTrack(track)}
                        className={cn("grid size-7 place-items-center rounded-full border transition", choice?.enabled ? "border-primary bg-primary text-primary-foreground" : "border-white/13 text-transparent hover:border-white/30", track.alternatives.length === 0 && "cursor-not-allowed opacity-30")}
                      >
                        <CheckIcon className="size-3.5" weight="bold" />
                      </button>

                      <div className="min-w-0">
                        <p className="truncate text-sm font-extrabold">{track.title}</p>
                        <p className="mt-0.5 truncate text-xs text-muted-foreground">{track.artist ?? "Sanatçı bulunamadı"}</p>
                        <p className="mt-1 truncate text-[10px] text-muted-foreground/60">{track.source}</p>
                      </div>

                      {chosen ? (
                        <div className="col-span-2 row-start-2 flex min-w-0 items-center gap-3 pl-9 sm:col-span-1 sm:row-start-auto sm:pl-0">
                          <Artwork track={chosen} />
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-1.5">
                              <p className="truncate text-sm font-bold">{chosen.name}</p>
                              {chosen.explicit && <span className="grid size-3.5 shrink-0 place-items-center rounded-[3px] bg-white/15 text-[8px] font-black text-muted-foreground">E</span>}
                            </div>
                            <p className="mt-0.5 truncate text-xs text-muted-foreground">{chosen.artist}</p>
                            <p className="mt-1 text-[10px] font-bold text-emerald-300">%{confidence(chosen.matchScore)} eşleşme</p>
                          </div>
                        </div>
                      ) : (
                        <div className="col-span-2 row-start-2 flex items-center gap-2 pl-9 text-xs font-semibold text-amber-300 sm:col-span-1 sm:row-start-auto sm:pl-0">
                          <MusicNotesIcon className="size-4" weight="duotone" /> Eşleşme bulunamadı
                        </div>
                      )}

                      <span className="hidden text-xs tabular-nums text-muted-foreground sm:block">{chosen ? formatDuration(chosen.durationMs) : "—"}</span>
                      <button
                        type="button"
                        disabled={track.alternatives.length < 2}
                        onClick={() => setExpandedTrack(expanded ? null : track.id)}
                        className="grid size-8 place-items-center rounded-full text-muted-foreground transition hover:bg-white/7 hover:text-foreground disabled:cursor-default disabled:opacity-25"
                        aria-label="Eşleşme alternatiflerini göster"
                        aria-expanded={expanded}
                      >
                        <CaretDownIcon className={cn("size-4 transition", expanded && "rotate-180")} weight="bold" />
                      </button>
                    </div>

                    <AnimatePresence>
                      {expanded && (
                        <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: "auto", opacity: 1 }} exit={{ height: 0, opacity: 0 }} className="overflow-hidden">
                          <div className="border-t border-white/5 bg-black/15 px-4 py-3 sm:pl-[82px]">
                            <p className="mb-2 text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground">Diğer Spotify sonuçları</p>
                            <div className="space-y-1">
                              {track.alternatives.map((alternative) => (
                                <button
                                  key={alternative.uri}
                                  type="button"
                                  onClick={() => chooseAlternative(track, alternative)}
                                  className={cn("flex w-full items-center gap-3 rounded-xl p-2 text-left transition hover:bg-white/6", alternative.uri === choice?.uri && "bg-primary/7")}
                                >
                                  <Artwork track={alternative} small />
                                  <div className="min-w-0 flex-1">
                                    <p className="truncate text-xs font-bold">{alternative.name}</p>
                                    <p className="truncate text-[11px] text-muted-foreground">{alternative.artist} · {alternative.album}</p>
                                  </div>
                                  <Badge variant={alternative.uri === choice?.uri ? "default" : "secondary"}>%{confidence(alternative.matchScore)}</Badge>
                                </button>
                              ))}
                            </div>
                          </div>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                )
              })}
            </div>
          </CardContent>
        </Card>

        <Card className="xl:sticky xl:top-24">
          <CardHeader className="border-b border-white/7">
            <div className="flex items-center justify-between">
              <CardTitle>Playlist'e gönder</CardTitle>
              <Badge variant="secondary">{selectedUris.length} parça</Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-5 p-6">
            <div>
              <label className="mb-2 block text-xs font-bold text-muted-foreground">Hedef</label>
              <Select value={playlistMode} onValueChange={setPlaylistMode}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="new">Yeni playlist oluştur</SelectItem>
                  {playlists.data?.map((playlist) => <SelectItem key={playlist.id} value={playlist.id}>{playlist.name} · {playlist.trackCount}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>

            {playlistMode === "new" && (
              <div>
                <label htmlFor="playlist-name" className="mb-2 block text-xs font-bold text-muted-foreground">Playlist adı</label>
                <Input id="playlist-name" value={playlistName} maxLength={100} onChange={(event) => setPlaylistName(event.target.value)} />
              </div>
            )}

            {playlistMode === "new" && (
              <button type="button" role="switch" aria-checked={isPublic} onClick={() => setIsPublic((current) => !current)} className="flex w-full items-center justify-between rounded-xl border border-white/7 bg-white/3 p-3 text-left">
                <div>
                  <p className="text-xs font-bold">Herkese açık</p>
                  <p className="mt-1 text-[10px] text-muted-foreground">Spotify profilinde görünsün</p>
                </div>
                <span className={cn("relative h-6 w-11 rounded-full transition", isPublic ? "bg-primary" : "bg-white/12")}><span className={cn("absolute top-1 size-4 rounded-full bg-white transition", isPublic ? "left-6" : "left-1")} /></span>
              </button>
            )}

            <div className="rounded-xl bg-primary/[0.065] p-3.5">
              <div className="flex items-center gap-2 text-xs font-bold text-primary"><ShieldCheckIcon className="size-4" weight="fill" /> Sonuç senin kontrolünde</div>
              <p className="mt-1.5 text-[11px] leading-5 text-muted-foreground">Yalnızca seçtiğin parçalar Spotify'a gönderilir.</p>
            </div>

            <Button className="w-full" size="lg" disabled={selectedUris.length === 0 || exportPlaylist.isPending || (playlistMode === "new" && !playlistName.trim())} onClick={() => exportPlaylist.mutate()}>
              {exportPlaylist.isPending ? <ArrowClockwiseIcon className="animate-spin" weight="bold" /> : <PlaylistIcon weight="fill" />}
              {exportPlaylist.isPending ? "Gönderiliyor…" : "Playlist'i hazırla"}
            </Button>
            <p className="text-center text-[10px] font-semibold text-muted-foreground">Spotify hesabında doğrudan oluşturulur</p>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function Artwork({ track, small = false }: { track: SpotifyTrack; small?: boolean }) {
  const size = small ? "size-9 rounded-md" : "size-11 rounded-lg"
  if (track.imageUrl) {
    return (
      <a href={track.externalUrl ?? undefined} target="_blank" rel="noreferrer" className="shrink-0" aria-label={`${track.name} parçasını Spotify'da aç`}>
        <img src={track.imageUrl} alt="" className={cn(size, "object-cover")} />
      </a>
    )
  }
  return <span className={cn(size, "grid shrink-0 place-items-center bg-white/7")}><PlaylistIcon className="size-4 text-muted-foreground" weight="duotone" /></span>
}

function confidence(score: number) {
  return Math.max(25, Math.min(99, Math.round((score / 270) * 100)))
}
