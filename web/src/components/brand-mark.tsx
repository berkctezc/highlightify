import { WaveformIcon } from "@/components/icons"

import { cn } from "@/lib/utils"

export function BrandMark({ compact = false, className }: { compact?: boolean; className?: string }) {
  return (
    <div className={cn("flex items-center gap-3", className)} aria-label="Highlightify">
      <span className="grid size-9 shrink-0 place-items-center overflow-hidden rounded-[11px] bg-white text-black">
        <WaveformIcon className="size-[18px]" weight="bold" />
      </span>
      {!compact && (
        <span className="text-[16px] font-extrabold tracking-[-0.045em]">highlightify</span>
      )}
    </div>
  )
}
