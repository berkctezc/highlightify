import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { motion } from "motion/react"
import { AlertTriangle, ArrowLeft, CheckCircle2, ExternalLink, RefreshCw, Search, Sparkles } from "lucide-react"
import { Link, useLocation, useParams } from "react-router-dom"
import { toast } from "sonner"

import { api } from "@/api/client"
import type { ImportJob, ImportStatus } from "@/api/types"
import { ImportProgress } from "@/components/import/import-progress"
import { MatchReview } from "@/components/import/match-review"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"

const activeStatuses: ImportStatus[] = ["queued", "reading", "matching", "exporting"]

export function ImportPage() {
  const { id } = useParams<{ id: string }>()
  const location = useLocation()
  const queryClient = useQueryClient()
  const spotify = useSpotifyConnection()
  const spotifyActions = useSpotifyActions()
  const job = useQuery({
    queryKey: ["import", id],
    queryFn: ({ signal }) => api.getImport(id!, signal),
    enabled: Boolean(id),
    refetchInterval: (query) => activeStatuses.includes(query.state.data?.status as ImportStatus) ? 900 : false,
  })
  const retryMatching = useMutation({
    mutationFn: () => api.retryMatching(id!),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ["import", id] }),
    onError: (error: Error) => toast.error(error.message),
  })

  if (job.isLoading) return <ImportPageSkeleton />
  if (job.isError || !job.data) {
    return <ErrorState title="Aktarım açılamadı" message={(job.error as Error)?.message ?? "Bu aktarım artık mevcut olmayabilir."} />
  }

  if (activeStatuses.includes(job.data.status)) return <ImportProgress job={job.data} />
  if (job.data.status === "failed") return <ErrorState title="Aktarım tamamlanamadı" message={job.data.error ?? "Beklenmeyen bir sorun oluştu."} />
  if (job.data.status === "completed") return <CompletedState job={job.data} />

  const hasAlternatives = job.data.tracks.some((track) => track.alternatives.length > 0)
  if (!hasAlternatives) {
    return (
      <div className="mx-auto max-w-2xl py-10 sm:py-20">
        <Card className="overflow-hidden text-center">
          <CardContent className="p-7 sm:p-12">
            <span className="mx-auto grid size-16 place-items-center rounded-2xl bg-primary/10 text-primary"><Search className="size-7" /></span>
            <Badge className="mt-6" variant="secondary">{job.data.tracks.length} müzik bulundu</Badge>
            <h2 className="mt-4 text-3xl font-extrabold tracking-[-0.045em]">Spotify eşleşmeleri hazır değil.</h2>
            <p className="mx-auto mt-3 max-w-md text-sm leading-6 text-muted-foreground">
              {spotify.data?.connected
                ? "Hesabın bağlı. Bulunan müzikleri Spotify kataloğuyla eşleştirmeyi başlatabilirsin."
                : "Bulunan müzikleri eşleştirmek ve playlist'e eklemek için Spotify hesabını bağla."}
            </p>
            <div className="mt-7 flex flex-col justify-center gap-3 sm:flex-row">
              {spotify.data?.connected ? (
                <Button size="lg" onClick={() => retryMatching.mutate()} disabled={retryMatching.isPending}>
                  {retryMatching.isPending ? <RefreshCw className="animate-spin" /> : <Sparkles />}
                  Eşleştirmeyi başlat
                </Button>
              ) : (
                <Button size="lg" onClick={() => spotifyActions.connect(`${location.pathname}`)}>Spotify'a bağlan</Button>
              )}
              <Button variant="outline" size="lg" asChild><Link to="/">Yeni aktarım</Link></Button>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  return <MatchReview job={job.data} />
}

function CompletedState({ job }: { job: ImportJob }) {
  return (
    <motion.div initial={{ opacity: 0, scale: 0.98 }} animate={{ opacity: 1, scale: 1 }} className="mx-auto max-w-3xl py-10 sm:py-20">
      <Card className="overflow-hidden border-primary/15 bg-primary/[0.035] text-center">
        <CardContent className="p-8 sm:p-14">
          <span className="mx-auto grid size-20 place-items-center rounded-full bg-primary text-primary-foreground shadow-[0_0_60px_-15px_var(--primary)]"><CheckCircle2 className="size-9" strokeWidth={2.6} /></span>
          <Badge className="mt-7" variant="success">Aktarım tamamlandı</Badge>
          <h2 className="text-balance mt-4 text-4xl font-extrabold tracking-[-0.055em] sm:text-6xl">Playlist'in hazır.</h2>
          <p className="mx-auto mt-4 max-w-md text-sm leading-6 text-muted-foreground">{job.statusMessage}. Spotify uygulamasından dinlemeye başlayabilirsin.</p>
          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            {job.playlistUrl && <Button size="lg" asChild><a href={job.playlistUrl} target="_blank" rel="noreferrer">Spotify'da aç <ExternalLink /></a></Button>}
            <Button size="lg" variant="outline" asChild><Link to="/">Yeni aktarım</Link></Button>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  )
}

function ErrorState({ title, message }: { title: string; message: string }) {
  return (
    <div className="mx-auto max-w-2xl py-10 sm:py-20">
      <Card className="border-red-400/12 bg-red-400/[0.035]">
        <CardContent className="p-8 text-center sm:p-12">
          <span className="mx-auto grid size-14 place-items-center rounded-2xl bg-red-400/10 text-red-300"><AlertTriangle className="size-6" /></span>
          <h2 className="mt-5 text-2xl font-extrabold tracking-[-0.035em]">{title}</h2>
          <p className="mx-auto mt-3 max-w-md text-sm leading-6 text-muted-foreground">{message}</p>
          <Button className="mt-7" variant="outline" asChild><Link to="/"><ArrowLeft /> Yeni aktarım</Link></Button>
        </CardContent>
      </Card>
    </div>
  )
}

function ImportPageSkeleton() {
  return (
    <div className="mx-auto max-w-4xl animate-pulse py-12">
      <div className="mx-auto h-7 w-32 rounded-full bg-white/6" />
      <div className="mx-auto mt-6 h-12 max-w-lg rounded-2xl bg-white/6" />
      <div className="mx-auto mt-4 h-5 max-w-md rounded-xl bg-white/4" />
      <div className="mt-12 h-64 rounded-[1.5rem] border border-white/6 bg-white/3" />
    </div>
  )
}
