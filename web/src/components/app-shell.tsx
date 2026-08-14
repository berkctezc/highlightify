import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"
import { cn } from "@/lib/utils"
import { CircleUserRound, Clock3, Home, LogOut, Plus, Settings } from "lucide-react"
import type { ReactNode } from "react"
import { NavLink, useLocation } from "react-router-dom"

import { BrandMark } from "@/components/brand-mark"

const navigation = [
  { to: "/", label: "Yeni aktarım", icon: Plus, exact: true },
  { to: "/history", label: "Geçmiş", icon: Clock3 },
  { to: "/settings", label: "Ayarlar", icon: Settings },
]

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation()
  const spotify = useSpotifyConnection()
  const actions = useSpotifyActions()
  const currentTitle = location.pathname.startsWith("/history")
    ? "Aktarım geçmişi"
    : location.pathname.startsWith("/settings")
      ? "Ayarlar"
      : location.pathname.startsWith("/imports")
        ? "Aktarım detayı"
        : "Yeni aktarım"

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[244px_minmax(0,1fr)]">
      <div className="app-grid pointer-events-none fixed inset-0 -z-10" />

      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[244px] flex-col border-r border-white/6 bg-black/18 px-4 py-5 backdrop-blur-xl lg:flex">
        <BrandMark className="px-2" />
        <nav className="mt-10 space-y-1.5" aria-label="Ana navigasyon">
          {navigation.map((item) => (
            <NavItem key={item.to} {...item} />
          ))}
        </nav>

        <div className="mt-auto rounded-2xl border border-white/7 bg-white/4 p-3">
          {spotify.data?.connected && spotify.data.profile ? (
            <div className="flex items-center gap-3">
              <Avatar>
                {spotify.data.profile.imageUrl && <AvatarImage src={spotify.data.profile.imageUrl} alt="" />}
                <AvatarFallback>{spotify.data.profile.displayName.slice(0, 2).toUpperCase()}</AvatarFallback>
              </Avatar>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-bold">{spotify.data.profile.displayName}</p>
                <p className="text-[11px] font-semibold text-emerald-300">Spotify bağlı</p>
              </div>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="size-8"
                    aria-label="Spotify bağlantısını kes"
                    onClick={() => actions.disconnect.mutate()}
                  >
                    <LogOut />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Bağlantıyı kes</TooltipContent>
              </Tooltip>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm font-bold">
                <CircleUserRound className="size-4 text-muted-foreground" /> Spotify bağlı değil
              </div>
              <Button className="w-full" size="sm" onClick={() => actions.connect(location.pathname)}>
                Spotify'a bağlan
              </Button>
            </div>
          )}
        </div>
      </aside>

      <div className="min-w-0 lg:col-start-2">
        <header className="sticky top-0 z-20 flex h-18 items-center justify-between border-b border-white/5 bg-background/72 px-4 backdrop-blur-2xl sm:px-7 lg:px-9">
          <div className="flex items-center gap-3">
            <BrandMark compact className="lg:hidden" />
            <div>
              <p className="text-[11px] font-bold uppercase tracking-[0.16em] text-muted-foreground">Highlightify</p>
              <h1 className="text-base font-extrabold tracking-[-0.025em]">{currentTitle}</h1>
            </div>
          </div>
          <div className="lg:hidden">
            {spotify.data?.connected && spotify.data.profile ? (
              <Avatar className="size-9 ring-2 ring-emerald-400/25">
                {spotify.data.profile.imageUrl && <AvatarImage src={spotify.data.profile.imageUrl} alt="" />}
                <AvatarFallback>{spotify.data.profile.displayName.slice(0, 2).toUpperCase()}</AvatarFallback>
              </Avatar>
            ) : (
              <Button size="sm" onClick={() => actions.connect(location.pathname)}>Bağlan</Button>
            )}
          </div>
        </header>

        <main className="mx-auto w-full max-w-[1240px] px-4 pb-28 pt-6 sm:px-7 sm:pt-9 lg:px-9 lg:pb-12">
          {children}
        </main>
      </div>

      <nav className="safe-bottom fixed inset-x-3 bottom-3 z-40 grid grid-cols-3 rounded-2xl border border-white/10 bg-[#151815]/92 p-1.5 shadow-2xl backdrop-blur-2xl lg:hidden" aria-label="Mobil navigasyon">
        {navigation.map((item) => (
          <NavItem key={item.to} {...item} mobile />
        ))}
      </nav>
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
  icon: typeof Home
  exact?: boolean
  mobile?: boolean
}) {
  return (
    <NavLink
      to={to}
      end={exact}
      className={({ isActive }) => cn(
        "group flex items-center rounded-xl font-bold transition",
        mobile ? "flex-col justify-center gap-1 px-2 py-2 text-[10px]" : "gap-3 px-3 py-3 text-sm",
        isActive ? "bg-white/9 text-foreground" : "text-muted-foreground hover:bg-white/5 hover:text-foreground",
      )}
    >
      {({ isActive }) => (
        <>
          <Icon className={cn("size-[18px] transition", isActive && "text-primary")} strokeWidth={2.3} />
          {label}
        </>
      )}
    </NavLink>
  )
}
