import type { ReactNode } from "react"
import { NavLink, useLocation } from "react-router-dom"

import { BrandMark } from "@/components/brand-mark"
import {
  ArrowClockwiseIcon,
  CircleNotchIcon,
  ClockCounterClockwiseIcon,
  GearSixIcon,
  HouseIcon,
  PlusIcon,
  SignOutIcon,
  SpotifyLogoIcon,
} from "@/components/icons"
import { SpotifyConnectButton } from "@/components/spotify-connect-button"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"
import { cn } from "@/lib/utils"

const navigation = [
  { to: "/app", label: "New import", icon: PlusIcon, exact: true },
  { to: "/history", label: "History", icon: ClockCounterClockwiseIcon },
  { to: "/settings", label: "Settings", icon: GearSixIcon },
]

const routeMeta = {
  home: { title: "New import", description: "Import music from Instagram to Spotify" },
  history: { title: "History", description: "Your past imports and results" },
  settings: { title: "Settings", description: "Connections and local tools" },
  import: { title: "Import details", description: "Review your matches" },
}

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation()
  const spotify = useSpotifyConnection()
  const actions = useSpotifyActions()
  const meta = location.pathname.startsWith("/history")
    ? routeMeta.history
    : location.pathname.startsWith("/settings")
      ? routeMeta.settings
      : location.pathname.startsWith("/imports")
        ? routeMeta.import
        : routeMeta.home

  return (
    <div className="min-h-screen bg-background lg:grid lg:grid-cols-[236px_minmax(0,1fr)]">
      <a href="#main-content" className="fixed left-4 top-4 z-50 -translate-y-24 rounded-full bg-white px-4 py-2 text-sm font-extrabold text-black transition focus:translate-y-0">Skip to content</a>

      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[236px] flex-col bg-black px-3 py-5 lg:flex">
        <BrandMark className="px-3" />

        <nav className="mt-9 space-y-1" aria-label="Main navigation">
          {navigation.map((item) => <NavItem key={item.to} {...item} />)}
        </nav>

        <div className="mt-auto px-1 pb-1">
          <SpotifyAccount
            spotify={spotify}
            onRetry={() => spotify.refetch()}
            onDisconnect={() => actions.disconnect.mutate()}
            returnPath={location.pathname}
          />
          <p className="mt-4 px-3 text-[10px] font-semibold text-[#6a6a6a]">Highlightify · Runs on this device</p>
        </div>
      </aside>

      <div className="min-w-0 lg:col-start-2">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-white/[0.055] bg-background/90 px-4 backdrop-blur-xl sm:px-7 lg:px-8">
          <div className="flex min-w-0 items-center gap-3">
            <BrandMark compact className="lg:hidden" />
            <div className="min-w-0">
              <h1 className="truncate text-sm font-extrabold tracking-[-0.02em] sm:text-base">{meta.title}</h1>
              <p className="mt-0.5 hidden truncate text-[11px] text-muted-foreground sm:block">{meta.description}</p>
            </div>
          </div>

          <div className="lg:hidden">
            {spotify.isPending ? (
              <span className="grid size-9 place-items-center rounded-full bg-white/[0.06] text-muted-foreground" aria-label="Checking Spotify connection"><CircleNotchIcon className="size-4 animate-spin" weight="bold" /></span>
            ) : spotify.data?.connected && spotify.data.profile ? (
              <Avatar className="size-9 ring-2 ring-[#1ed760]/25">
                {spotify.data.profile.imageUrl && <AvatarImage src={spotify.data.profile.imageUrl} alt="" />}
                <AvatarFallback>{spotify.data.profile.displayName.slice(0, 2).toUpperCase()}</AvatarFallback>
              </Avatar>
            ) : spotify.isError ? (
              <Button size="icon" variant="ghost" aria-label="Retry Spotify connection check" onClick={() => spotify.refetch()}><ArrowClockwiseIcon weight="bold" /></Button>
            ) : (
              <SpotifyConnectButton size="sm" returnPath={location.pathname}>Connect</SpotifyConnectButton>
            )}
          </div>
        </header>

        <main id="main-content" className="mx-auto w-full max-w-[1320px] px-4 pb-28 pt-8 sm:px-7 sm:pt-10 lg:px-8 lg:pb-16">
          {children}
        </main>
      </div>

      <nav className="safe-bottom fixed inset-x-3 bottom-3 z-40 grid grid-cols-3 rounded-[18px] border border-white/10 bg-[#0a0a0a]/94 p-1.5 shadow-[0_20px_60px_rgba(0,0,0,.55)] backdrop-blur-2xl lg:hidden" aria-label="Mobile navigation">
        {navigation.map((item) => <NavItem key={item.to} {...item} mobile />)}
      </nav>
    </div>
  )
}

type SpotifyQuery = ReturnType<typeof useSpotifyConnection>

function SpotifyAccount({
  spotify,
  onRetry,
  onDisconnect,
  returnPath,
}: {
  spotify: SpotifyQuery
  onRetry: () => void
  onDisconnect: () => void
  returnPath: string
}) {
  if (spotify.isPending) {
    return (
      <div className="flex items-center gap-3 rounded-xl bg-[#181818] p-3" role="status">
        <CircleNotchIcon className="size-4 animate-spin text-muted-foreground" weight="bold" />
        <span className="text-xs font-bold text-muted-foreground">Checking Spotify</span>
      </div>
    )
  }

  if (spotify.data?.connected && spotify.data.profile) {
    return (
      <div className="flex items-center gap-3 rounded-xl bg-[#181818] p-3">
        <Avatar className="size-9">
          {spotify.data.profile.imageUrl && <AvatarImage src={spotify.data.profile.imageUrl} alt="" />}
          <AvatarFallback>{spotify.data.profile.displayName.slice(0, 2).toUpperCase()}</AvatarFallback>
        </Avatar>
        <div className="min-w-0 flex-1">
          <p className="truncate text-xs font-extrabold">{spotify.data.profile.displayName}</p>
          <p className="mt-0.5 flex items-center gap-1.5 text-[10px] font-bold text-[#1ed760]"><span className="size-1.5 rounded-full bg-current" /> Spotify connected</p>
        </div>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button size="icon" variant="ghost" className="size-8" aria-label="Disconnect Spotify" onClick={onDisconnect}><SignOutIcon weight="bold" /></Button>
          </TooltipTrigger>
          <TooltipContent>Disconnect</TooltipContent>
        </Tooltip>
      </div>
    )
  }

  if (spotify.isError) {
    return (
      <button type="button" onClick={onRetry} className="flex w-full items-center gap-3 rounded-xl bg-[#181818] p-3 text-left transition hover:bg-[#242424]">
        <ArrowClockwiseIcon className="size-4 text-muted-foreground" weight="bold" />
        <span className="text-xs font-bold">Retry connection check</span>
      </button>
    )
  }

  return (
    <div className="rounded-xl bg-[#181818] p-3">
      <div className="flex items-center gap-2 text-xs font-bold"><SpotifyLogoIcon className="size-[18px] text-white" weight="fill" /> Spotify not connected</div>
      <SpotifyConnectButton className="mt-3 w-full" size="sm" returnPath={returnPath}>Connect Spotify</SpotifyConnectButton>
    </div>
  )
}

function NavItem({
  to,
  label,
  icon: Icon,
  exact,
  mobile = false,
}: {
  to: string
  label: string
  icon: typeof HouseIcon
  exact?: boolean
  mobile?: boolean
}) {
  return (
    <NavLink
      to={to}
      end={exact}
      className={({ isActive }) => cn(
        "flex items-center rounded-lg font-bold transition-colors",
        mobile ? "flex-col justify-center gap-1 px-2 py-2 text-[10px]" : "gap-3 px-3 py-3 text-sm",
        isActive ? "bg-[#282828] text-white" : "text-[#a7a7a7] hover:bg-[#181818] hover:text-white",
      )}
    >
      {({ isActive }) => (
        <>
          <Icon className={cn("size-[19px]", isActive && "text-primary")} weight={isActive ? "fill" : "regular"} />
          <span>{label}</span>
        </>
      )}
    </NavLink>
  )
}