import { describe, expect, it } from "vitest"

import { cn, formatDuration } from "@/lib/utils"

describe("formatDuration", () => {
  it("formats Spotify durations as minutes and seconds", () => {
    expect(formatDuration(183_000)).toBe("3:03")
  })

  it("uses a dash for unavailable durations", () => {
    expect(formatDuration(0)).toBe("—")
  })
})

describe("cn", () => {
  it("merges conflicting Tailwind classes", () => {
    expect(cn("px-2", "px-4")).toBe("px-4")
  })
})
