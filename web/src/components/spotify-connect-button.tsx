import type { ComponentProps } from "react"

import { SpotifyLogoIcon } from "@/components/icons"
import { Button } from "@/components/ui/button"
import { spotifyLoginUrl } from "@/api/client"

type SpotifyConnectButtonProps = Omit<ComponentProps<typeof Button>, "onClick" | "asChild"> & {
  returnPath?: string
}

export function SpotifyConnectButton({ returnPath = "/", children, ...buttonProps }: SpotifyConnectButtonProps) {
  return (
    <Button {...buttonProps} onClick={() => window.location.assign(spotifyLoginUrl(returnPath))}>
      {children ?? <><SpotifyLogoIcon weight="fill" /> Connect Spotify</>}
    </Button>
  )
}
