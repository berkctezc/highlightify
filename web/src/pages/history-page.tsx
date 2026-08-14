import { useQuery } from "@tanstack/react-query"
import { ArrowRight, CheckCircle2, Clock3, Disc3, Music2, Plus, TriangleAlert } from "lucide-react"
import { Link } from "react-router-dom"

import { api } from "@/api/client"
import type { ImportJob, ImportStatus } from "@/api/types"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { formatRelativeDate } from "@/lib/utils"

const statusMeta: Record<ImportStatus, { label: string; variant: "default" | "secondary" | "success" | "warning" | "destructive" }> = {
  queued: { label: "Sırada", variant: "secondary" },
  reading: { label: "Okunuyor", variant: "warning" },
  matching: { label: "Eşleşiyor", variant: "warning" },
  ready: { label: "Kontrol bekliyor", variant: "default" },
  exporting: { label: "Gönderiliyor", variant: "warning" },
  completed: { label: "Tamamlandı", variant: "success" },
  failed: { label: "Başarısız", variant: "destructive" },
}

export function HistoryPage() {
  const imports = useQuery({
    queryKey: ["imports"],
    queryFn: ({ signal }) => api.getImports(signal),
    refetchInterval: (query) => query.state.data?.some((job) => ["queued", "reading", "matching", "exporting"].includes(job.status)) ? 1_500 : false,
  })

  return (
    <div>
      <div className="mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <Badge variant="secondary"><Clock3 className="size-3" /> Son işlemler</Badge>
          <h2 className="mt-4 text-3xl font-extrabold tracking-[-0.045em] sm:text-5xl">Müzik yolculuğun.</h2>
          <p className="mt-3 text-sm text-muted-foreground">Aktarımlarını kontrol et ve yarım kalan yerden devam et.</p>
        </div>
        <Button asChild><Link to="/"><Plus /> Yeni aktarım</Link></Button>
      </div>

      {imports.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 3 }).map((_, index) => <div key={index} className="h-56 animate-pulse rounded-[1.5rem] border border-white/6 bg-white/3" />)}</div>
      ) : imports.data?.length ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {imports.data.map((job) => <HistoryCard key={job.id} job={job} />)}
        </div>
      ) : (
        <Card className="border-dashed bg-white/[0.018]">
          <CardContent className="flex min-h-80 flex-col items-center justify-center p-8 text-center">
            <span className="grid size-16 place-items-center rounded-2xl bg-white/5 text-muted-foreground"><Music2 className="size-7" /></span>
            <h3 className="mt-5 text-xl font-extrabold">Henüz bir aktarım yok.</h3>
            <p className="mt-2 max-w-sm text-sm leading-6 text-muted-foreground">İlk Instagram Highlight'ını eklediğinde süreç burada görünecek.</p>
            <Button className="mt-6" asChild><Link to="/">İlk aktarımı başlat <ArrowRight /></Link></Button>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function HistoryCard({ job }: { job: ImportJob }) {
  const meta = statusMeta[job.status]
  const matched = job.tracks.filter((track) => track.match).length
  return (
    <Link to={`/imports/${job.id}`} className="group block rounded-[1.5rem] outline-none focus-visible:ring-2 focus-visible:ring-primary">
      <Card className="h-full transition duration-300 group-hover:-translate-y-1 group-hover:border-white/13 group-hover:bg-[#151815]">
        <CardContent className="p-5">
          <div className="flex items-start justify-between gap-3">
            <span className="grid size-11 place-items-center rounded-xl bg-white/6 text-muted-foreground">
              {job.status === "completed" ? <CheckCircle2 className="size-5 text-primary" /> : job.status === "failed" ? <TriangleAlert className="size-5 text-red-300" /> : <Disc3 className="size-5" />}
            </span>
            <Badge variant={meta.variant}>{meta.label}</Badge>
          </div>
          <h3 className="mt-5 truncate text-lg font-extrabold tracking-[-0.025em]">{job.sources[0] ?? "Instagram Highlight"}</h3>
          <p className="mt-1 text-xs text-muted-foreground">{job.sources.length} kaynak · {job.tracks.length} aday · {matched} eşleşme</p>
          <div className="mt-7 flex items-center justify-between border-t border-white/6 pt-4">
            <span className="text-[11px] font-semibold text-muted-foreground">{formatRelativeDate(job.createdAt)}</span>
            <span className="flex items-center gap-1 text-xs font-bold text-foreground">Detay <ArrowRight className="size-3.5 transition group-hover:translate-x-1" /></span>
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
