import type * as React from "react"

import { cn } from "@/lib/utils"

function Textarea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        "min-h-28 w-full resize-y rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-[15px] leading-6 text-foreground outline-none transition placeholder:text-muted-foreground/65 focus:border-primary/55 focus:bg-white/7 focus:ring-3 focus:ring-primary/10 disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  )
}

export { Textarea }
