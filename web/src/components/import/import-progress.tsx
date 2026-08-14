import { motion } from "motion/react"
import { CheckIcon, ImagesIcon, MagnifyingGlassIcon, VinylRecordIcon, WaveformIcon } from "@/components/icons"

import type { ImportJob } from "@/api/types"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent } from "@/components/ui/card"
import { Progress } from "@/components/ui/progress"
import { cn } from "@/lib/utils"

const steps = [
  { key: "source", label: "Kaynaklar", icon: ImagesIcon, threshold: 8 },
  { key: "read", label: "Müzikler", icon: WaveformIcon, threshold: 35 },
  { key: "match", label: "Eşleşmeler", icon: MagnifyingGlassIcon, threshold: 88 },
  { key: "ready", label: "Hazır", icon: VinylRecordIcon, threshold: 100 },
]

export function ImportProgress({ job }: { job: ImportJob }) {
  return (
    <div className="mx-auto max-w-4xl py-6 sm:py-12">
      <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="text-center">
        <Badge><WaveformIcon className="size-3" weight="bold" /> Aktarım çalışıyor</Badge>
        <h2 className="type-page-title mt-5">Highlight'ların dinleniyor.</h2>
        <p className="mx-auto mt-4 max-w-xl text-sm leading-6 text-muted-foreground sm:text-base">Sekmeyi açık tutabilirsin; bulunan müzikleri adım adım Spotify kataloğuyla eşleştiriyoruz.</p>
      </motion.div>

      <Card className="mt-9 overflow-hidden sm:mt-12">
        <CardContent className="p-6 sm:p-9">
          <div className="mb-7 flex items-end justify-between gap-4">
            <div>
              <p className="text-sm font-extrabold">{job.statusMessage}</p>
              <p className="mt-1 text-xs text-muted-foreground">{job.sources.length} kaynak · {job.tracks.length} müzik adayı</p>
            </div>
            <span className="text-3xl font-extrabold tracking-[-0.055em] text-primary">{job.progress}%</span>
          </div>
          <Progress value={job.progress} className="h-2" />

          <div className="mt-9 grid grid-cols-4 gap-2">
            {steps.map((step, index) => {
              const complete = job.progress >= step.threshold
              const active = !complete && (index === 0 || job.progress >= steps[index - 1].threshold)
              const Icon = step.icon
              return (
                <div key={step.key} className="relative text-center">
                  {index > 0 && <span className={cn("absolute right-1/2 top-4 h-px w-full -translate-y-1/2", job.progress >= steps[index - 1].threshold ? "bg-primary/45" : "bg-white/8")} />}
                  <span className={cn(
                    "relative z-10 mx-auto grid size-8 place-items-center rounded-full border transition",
                    complete ? "border-primary bg-primary text-primary-foreground" : active ? "border-primary/55 bg-primary/10 text-primary" : "border-white/8 bg-card text-muted-foreground",
                  )}>
                    {complete ? <CheckIcon className="size-4" weight="bold" /> : <Icon className={cn("size-4", active && "animate-pulse")} weight={active ? "duotone" : "regular"} />}
                  </span>
                  <p className={cn("mt-2.5 text-[10px] font-bold sm:text-xs", complete || active ? "text-foreground" : "text-muted-foreground")}>{step.label}</p>
                </div>
              )
            })}
          </div>
        </CardContent>
      </Card>

      <div className="mt-5 grid gap-3 sm:grid-cols-3">
        {job.sources.slice(0, 3).map((source, index) => (
          <motion.div
            key={`${source}-${index}`}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.08 }}
            className="flex min-w-0 items-center gap-3 rounded-2xl border border-white/6 bg-white/3 p-3"
          >
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white/6"><ImagesIcon className="size-4 text-accent" weight="duotone" /></span>
            <span className="truncate text-xs font-semibold text-muted-foreground">{source}</span>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
