import { useQuery } from "@tanstack/react-query"
import { Link } from "react-router-dom"

import { api } from "@/api/client"
import type { ImportJob, ImportStatus } from "@/api/types"
import {
  ArrowRightIcon,
  CheckCircleIcon,
  CircleNotchIcon,
  ClockCounterClockwiseIcon,
  MusicNotesIcon,
  PlusIcon,
  VinylRecordIcon,
  WarningIcon,
} from "@/components/icons"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { formatRelativeDate } from "@/lib/utils"

const statusMeta: Record<ImportStatus, { label: string; variant: "default" | "secondary" | "success" | "warning" | "destructive" }> = {
  queued: { label: "Queued", variant: "secondary" },
  reading: { label: "Reading", variant: "warning" },
  matching: { label: "Matching", variant: "warning" },
  ready: { label: "Ready for review", variant: "default" },
  exporting: { label: "Exporting", variant: "warning" },
  completed: { label: "Completed", variant: "success" },
  failed: { label: "Failed", variant: "destructive" },
}

export function HistoryPage() {
  const imports = useQuery({
    queryKey: ["imports"],
    queryFn: ({ signal }) => api.getImports(signal),
    refetchInterval: (query) => query.state.data?.some((job) => ["queued", "reading", "matching", "exporting"].includes(job.status)) ? 1_500 : false,
  })

  return (
    <div className="mx-auto max-w-5xl">
      <header className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="type-eyebrow flex items-center gap-2 text-primary"><ClockCounterClockwiseIcon className="size-4" weight="bold" /> Import history</p>
          <h2 className="type-page-title mt-3">Pick up where you left off.</h2>
          <p className="type-body mt-3">Open results, edit matches, or return to a completed playlist.</p>
        </div>
        <Button asChild><Link to="/app"><PlusIcon weight="bold" /> New import</Link></Button>
      </header>

      <section className="mt-8" aria-label="Imports">
        {imports.isLoading ? (
          <div className="overflow-hidden rounded-2xl border border-white/[0.07] bg-[#181818]">
            {Array.from({ length: 4 }).map((_, index) => <div key={index} className="flex animate-pulse items-center gap-4 border-b border-white/[0.06] p-4 last:border-b-0 sm:p-5"><div className="size-11 rounded-lg bg-white/[0.06]" /><div className="flex-1"><div className="h-3 w-40 rounded-full bg-white/[0.07]" /><div className="mt-2 h-2.5 w-56 rounded-full bg-white/[0.04]" /></div></div>)}
          </div>
        ) : imports.data?.length ? (
          <div className="overflow-hidden rounded-2xl border border-white/[0.07] bg-[#181818]">
            {imports.data.map((job) => <HistoryRow key={job.id} job={job} />)}
          </div>
        ) : (
          <div className="grid min-h-80 place-items-center rounded-2xl border border-dashed border-white/[0.1] bg-[#181818]/60 p-8 text-center">
            <div>
              <span className="mx-auto grid size-14 place-items-center rounded-full bg-white/[0.06] text-muted-foreground"><MusicNotesIcon className="size-6" weight="duotone" /></span>
              <h3 className="mt-5 text-lg font-extrabold">No imports yet</h3>
              <p className="mx-auto mt-2 max-w-sm text-xs leading-5 text-muted-foreground">Your first job will appear here when you add your first Instagram link.</p>
              <Button className="mt-6" asChild><Link to="/app">Start your first import <ArrowRightIcon weight="bold" /></Link></Button>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}

function HistoryRow({ job }: { job: ImportJob }) {
  const meta = statusMeta[job.status]
  const matched = job.tracks.filter((track) => track.match).length
  const working = ["queued", "reading", "matching", "exporting"].includes(job.status)

  return (
    <Link to={`/imports/${job.id}`} className="group flex items-center gap-3 border-b border-white/[0.06] p-4 outline-none transition last:border-b-0 hover:bg-[#222222] focus-visible:bg-[#222222] sm:gap-4 sm:p-5">
      <span className="grid size-11 shrink-0 place-items-center rounded-lg bg-[#282828] text-muted-foreground">
        {job.status === "completed" ? <CheckCircleIcon className="size-5 text-primary" weight="fill" /> : job.status === "failed" ? <WarningIcon className="size-5 text-red-300" weight="fill" /> : working ? <CircleNotchIcon className="size-5 animate-spin" weight="bold" /> : <VinylRecordIcon className="size-5" weight="duotone" />}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2"><h3 className="truncate text-sm font-extrabold">{sourceLabel(job)}</h3><span className="hidden text-[10px] text-muted-foreground sm:inline">· {formatRelativeDate(job.createdAt)}</span></div>
        <p className="mt-1 truncate text-[11px] text-muted-foreground">{job.sources.length} sources · {job.tracks.length} tracks · {matched} matches</p>
      </div>
      <Badge className="hidden sm:inline-flex" variant={meta.variant}>{meta.label}</Badge>
      <ArrowRightIcon className="size-4 shrink-0 text-muted-foreground transition group-hover:translate-x-1 group-hover:text-white" weight="bold" />
    </Link>
  )
}

function sourceLabel(job: ImportJob) {
  const source = job.sources[0]
  if (!source) return "Instagram import"
  try {
    const segments = new URL(source).pathname.split("/").filter(Boolean)
    const storiesIndex = segments.indexOf("stories")
    if (storiesIndex >= 0 && segments[storiesIndex + 1] && segments[storiesIndex + 1] !== "highlights") return `@${segments[storiesIndex + 1]}`
  } catch {
    // Highlight IDs and local HTML sources use the neutral label below.
  }
  return "Instagram import"
}
