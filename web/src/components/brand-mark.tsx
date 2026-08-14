import { Highlighter, Sparkles } from "lucide-react"

import { cn } from "@/lib/utils"

export function BrandMark({ compact = false, className }: { compact?: boolean; className?: string }) {
  return (
    <div className={cn("flex items-center gap-3", className)} aria-label="Highlightify">
      <span className="relative grid size-10 shrink-0 place-items-center overflow-hidden rounded-[14px] bg-primary text-primary-foreground shadow-[0_14px_38px_-14px_rgba(217,255,104,.85)]">
        <Highlighter className="size-5" strokeWidth={2.6} />
        <Sparkles className="absolute right-0.5 top-0.5 size-3" strokeWidth={2.5} />
      </span>
      {!compact && (
        <span className="text-[17px] font-extrabold tracking-[-0.045em]">
          highlight<span className="text-primary">ify</span>
        </span>
      )}
    </div>
  )
}
