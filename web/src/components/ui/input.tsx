import type * as React from "react"

import { cn } from "@/lib/utils"

function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        "h-12 w-full min-w-0 rounded-xl border border-white/10 bg-white/5 px-4 text-[15px] text-foreground outline-none transition placeholder:text-muted-foreground/65 file:mr-3 file:border-0 file:bg-transparent file:text-sm file:font-semibold focus:border-primary/55 focus:bg-white/7 focus:ring-3 focus:ring-primary/10 disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  )
}

export { Input }
