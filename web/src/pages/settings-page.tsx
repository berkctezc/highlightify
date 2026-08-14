import { useQuery } from "@tanstack/react-query"

import { api } from "@/api/client"
import {
  ArrowClockwiseIcon,
  ArrowSquareOutIcon,
  CheckCircleIcon,
  CircleNotchIcon,
  CloudIcon,
  KeyIcon,
  LaptopIcon,
  ShieldCheckIcon,
  SignOutIcon,
  SpotifyLogoIcon,
  TerminalWindowIcon,
  XCircleIcon,
} from "@/components/icons"
import { SpotifyConnectButton } from "@/components/spotify-connect-button"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"

export function SettingsPage() {
  const config = useQuery({ queryKey: ["config"], queryFn: ({ signal }) => api.getConfiguration(signal) })
  const spotify = useSpotifyConnection()
  const actions = useSpotifyActions()

  return (
    <div className="mx-auto max-w-5xl">
      <header>
        <p className="type-eyebrow flex items-center gap-2 text-primary"><ShieldCheckIcon className="size-4" weight="fill" /> Sistem durumu</p>
        <h2 className="type-page-title mt-3">Bağlantılar ve tercihler.</h2>
        <p className="type-body mt-3 max-w-2xl">Hesap bağlantını ve cihazında çalışan yerel yardımcıları tek yerden yönet.</p>
      </header>

      <section className="mt-8 overflow-hidden rounded-2xl border border-white/[0.07] bg-[#181818]" aria-label="Bağlantılar">
        <SettingRow
          icon={<span className="grid size-11 place-items-center rounded-full bg-black"><SpotifyLogoIcon className="size-6 text-[#1ed760]" weight="fill" /></span>}
          title={spotify.data?.profile?.displayName ?? "Spotify"}
          description={spotify.data?.connected ? "Playlist oluşturma yetkin bu cihazda şifreli olarak saklanıyor." : "Eşleşmeleri bulmak ve playlist oluşturmak için hesabını bağla."}
          status={spotify.isPending
            ? <Badge variant="secondary"><CircleNotchIcon className="animate-spin" weight="bold" /> Kontrol ediliyor</Badge>
            : spotify.isError
              ? <Badge variant="warning"><XCircleIcon weight="fill" /> Erişilemedi</Badge>
              : <StatusBadge ready={Boolean(spotify.data?.connected)} readyLabel="Bağlı" waitingLabel="Bağlı değil" />}
          action={spotify.isPending
            ? <Button variant="outline" disabled><CircleNotchIcon className="animate-spin" weight="bold" /> Bekleyin</Button>
            : spotify.isError
              ? <Button variant="outline" onClick={() => spotify.refetch()}><ArrowClockwiseIcon weight="bold" /> Tekrar dene</Button>
              : spotify.data?.connected
                ? <Button variant="outline" onClick={() => actions.disconnect.mutate()} disabled={actions.disconnect.isPending}><SignOutIcon weight="bold" /> Bağlantıyı kes</Button>
                : <SpotifyConnectButton returnPath="/settings" disabled={!spotify.data?.configured}><KeyIcon weight="bold" /> Spotify'a bağlan</SpotifyConnectButton>}
        >
          {!spotify.isPending && !spotify.data?.connected && <a href="https://support.spotify.com/us/article/cannot-remember-login/" target="_blank" rel="noreferrer" className="mt-2 inline-flex items-center gap-1.5 text-[11px] font-bold text-muted-foreground transition hover:text-white">Doğru giriş yöntemini bul <ArrowSquareOutIcon className="size-3" weight="bold" /></a>}
        </SettingRow>

        <SettingRow
          icon={<span className="grid size-11 place-items-center rounded-xl bg-[#282828] text-white"><TerminalWindowIcon className="size-5" weight="duotone" /></span>}
          title="Instagram okuyucu"
          description="Özel Story ve Highlight'lar için seçtiğin tarayıcının yerel oturumunu kullanır."
          status={<StatusBadge ready={Boolean(config.data?.ytDlpAvailable)} readyLabel="Hazır" waitingLabel="Bulunamadı" />}
          action={<code className="rounded-lg bg-black/35 px-3 py-2 text-[11px] text-muted-foreground">yt-dlp · local</code>}
        />

        <SettingRow
          icon={<span className="grid size-11 place-items-center rounded-xl bg-[#282828] text-white"><LaptopIcon className="size-5" weight="duotone" /></span>}
          title="Desktop uygulaması"
          description="Aynı arayüzü macOS ve Windows üzerinde kullanmak için planlanan paket."
          status={<Badge variant="secondary"><CloudIcon weight="duotone" /> Planlandı</Badge>}
          action={<span className="text-xs font-bold text-muted-foreground">Sonraki sürüm</span>}
        />
      </section>

      <div className="mt-5 flex items-start gap-3 rounded-xl border border-white/[0.07] bg-[#181818]/70 p-4 text-xs leading-5 text-muted-foreground">
        <ShieldCheckIcon className="mt-0.5 size-4 shrink-0 text-primary" weight="duotone" />
        <p>Spotify parolan hiçbir zaman Highlightify ile paylaşılmaz. Yetkilendirme Spotify'ın güvenli sayfasında tamamlanır; Instagram çerezleri cihazından ayrılmaz.</p>
      </div>

      {!config.data?.spotifyConfigured && (
        <div className="mt-5 flex items-start gap-3 rounded-xl border border-amber-300/15 bg-amber-300/[0.04] p-4">
          <KeyIcon className="mt-0.5 size-4 shrink-0 text-amber-300" weight="duotone" />
          <div><p className="text-xs font-extrabold text-amber-200">Spotify Client ID gerekiyor</p><p className="mt-1 text-[11px] leading-5 text-muted-foreground"><code>SPOTIFY_CLIENT_ID</code> değerini ayarla ve callback olarak <code>http://127.0.0.1:5086/api/auth/spotify/callback</code> ekle.</p><a href="https://developer.spotify.com/dashboard" target="_blank" rel="noreferrer" className="mt-2 inline-flex items-center gap-1 text-[11px] font-bold text-amber-300 hover:underline">Spotify Dashboard <ArrowSquareOutIcon className="size-3" weight="bold" /></a></div>
        </div>
      )}
    </div>
  )
}

function SettingRow({
  icon,
  title,
  description,
  status,
  action,
  children,
}: {
  icon: React.ReactNode
  title: string
  description: string
  status: React.ReactNode
  action: React.ReactNode
  children?: React.ReactNode
}) {
  return (
    <div className="grid gap-4 border-b border-white/[0.06] p-5 last:border-b-0 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:p-6">
      <div className="flex min-w-0 items-start gap-4">
        {icon}
        <div className="min-w-0"><div className="flex flex-wrap items-center gap-2.5"><h3 className="text-sm font-extrabold">{title}</h3>{status}</div><p className="mt-1.5 max-w-2xl text-xs leading-5 text-muted-foreground">{description}</p>{children}</div>
      </div>
      <div className="pl-[60px] sm:pl-0">{action}</div>
    </div>
  )
}

function StatusBadge({ ready, readyLabel, waitingLabel }: { ready: boolean; readyLabel: string; waitingLabel: string }) {
  return <Badge variant={ready ? "success" : "warning"}>{ready ? <CheckCircleIcon weight="fill" /> : <XCircleIcon weight="fill" />}{ready ? readyLabel : waitingLabel}</Badge>
}
