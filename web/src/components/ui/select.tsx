import * as SelectPrimitive from "@radix-ui/react-select"
import { CaretDownIcon, CheckIcon } from "@/components/icons"
import type * as React from "react"

import { cn } from "@/lib/utils"

const Select = SelectPrimitive.Root
const SelectValue = SelectPrimitive.Value

function SelectTrigger({ className, children, ...props }: React.ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      className={cn(
        "flex h-12 w-full items-center justify-between rounded-xl border border-white/10 bg-white/5 px-4 text-sm font-medium outline-none transition focus:border-primary/55 focus:ring-3 focus:ring-primary/10 data-[placeholder]:text-muted-foreground",
        className,
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild><CaretDownIcon className="size-4 text-muted-foreground" weight="bold" /></SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  )
}

function SelectContent({ className, children, position = "popper", ...props }: React.ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        data-slot="select-content"
        position={position}
        className={cn(
          "z-50 min-w-[8rem] overflow-hidden rounded-xl border border-white/10 bg-popover p-1 text-popover-foreground shadow-2xl data-[state=open]:animate-in data-[state=closed]:animate-out",
          position === "popper" && "w-[var(--radix-select-trigger-width)] translate-y-1",
          className,
        )}
        {...props}
      >
        <SelectPrimitive.Viewport>{children}</SelectPrimitive.Viewport>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  )
}

function SelectItem({ className, children, ...props }: React.ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
      className={cn("relative flex cursor-pointer select-none items-center rounded-lg py-2.5 pr-8 pl-3 text-sm outline-none data-[highlighted]:bg-white/8 data-[disabled]:opacity-50", className)}
      {...props}
    >
      <span className="absolute right-2 flex size-4 items-center justify-center"><SelectPrimitive.ItemIndicator><CheckIcon className="size-4" weight="bold" /></SelectPrimitive.ItemIndicator></span>
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
  )
}

export { Select, SelectContent, SelectItem, SelectTrigger, SelectValue }
