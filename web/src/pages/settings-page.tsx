import { useQuery } from "@tanstack/react-query"
import { CheckCircle2, Cloud, ExternalLink, KeyRound, Laptop2, LogOut, ShieldCheck, TerminalSquare, XCircle } from "lucide-react"

import { api } from "@/api/client"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"

export function SettingsPage() {
  const config = useQuery({ queryKey: ["config"], queryFn: ({ signal }) => api.getConfiguration(signal) })
  const spotify = useSpotifyConnection()
  const actions = useSpotifyActions()

  return (
    <div className="max-w-4xl">
      <Badge variant="secondary"><ShieldCheck className="size-3" /> Yerel ve güvenli</Badge>
      <h2 className="mt-4 text-3xl font-extrabold tracking-[-0.045em] sm:text-5xl">Bağlantılar ve sistem.</h2>
      <p className="mt-3 text-sm leading-6 text-muted-foreground">Highlightify'nin cihazındaki servislerle nasıl çalışacağını buradan takip et.</p>

      <div className="mt-8 space-y-4">
        <Card>
          <CardHeader className="flex-row items-start justify-between gap-5">
            <div>
              <CardTitle className="flex items-center gap-3"><span className="grid h-9 w-28 place-items-center rounded-xl bg-black/20 px-3"><img src="/spotify-full-logo-white.svg" alt="Spotify" className="h-auto w-[86px]" /></span></CardTitle>
              <CardDescription className="mt-2">Eşleşmeleri bulmak ve playlist oluşturmak için kullanılır.</CardDescription>
            </div>
            <StatusBadge ready={Boolean(spotify.data?.connected)} readyLabel="Bağlı" waitingLabel="Bağlı değil" />
          </CardHeader>
          <CardContent className="flex flex-col gap-4 border-t border-white/6 pt-5 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-bold">{spotify.data?.profile?.displayName ?? "Spotify hesabı bekleniyor"}</p>
              <p className="mt-1 text-xs text-muted-foreground">PKCE doğrulaması · parola Highlightify ile paylaşılmaz</p>
            </div>
            {spotify.data?.connected ? (
              <Button variant="outline" onClick={() => actions.disconnect.mutate()}><LogOut /> Bağlantıyı kes</Button>
            ) : (
              <Button onClick={() => actions.connect("/settings")} disabled={!spotify.data?.configured}><KeyRound /> Spotify'a bağlan</Button>
            )}
          </CardContent>
        </Card>

        <div className="grid gap-4 md:grid-cols-2">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between"><span className="grid size-10 place-items-center rounded-xl bg-primary/9 text-primary"><TerminalSquare className="size-5" /></span><StatusBadge ready={Boolean(config.data?.ytDlpAvailable)} readyLabel="Hazır" waitingLabel="Bulunamadı" /></div>
              <CardTitle className="mt-4">Instagram okuyucu</CardTitle>
              <CardDescription>Özel Highlight'lar için cihazındaki tarayıcı oturumunu yerel olarak kullanır.</CardDescription>
            </CardHeader>
            <CardContent>
              <code className="block rounded-xl bg-black/20 p-3 text-xs text-muted-foreground">yt-dlp --cookies-from-browser</code>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between"><span className="grid size-10 place-items-center rounded-xl bg-accent/9 text-accent"><Laptop2 className="size-5" /></span><Badge variant="secondary">Sonraki aşama</Badge></div>
              <CardTitle className="mt-4">Desktop uygulaması</CardTitle>
              <CardDescription>Aynı hızlı web arayüzü Tauri ile macOS ve Windows üzerinde paketlenebilir.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-2 text-xs font-semibold text-muted-foreground"><Cloud className="size-4" /> Web sürümü tamamlandıktan sonra eklenecek</div>
            </CardContent>
          </Card>
        </div>

        {!config.data?.spotifyConfigured && (
          <Card className="border-amber-300/15 bg-amber-300/[0.035]">
            <CardContent className="flex items-start gap-4 p-5">
              <KeyRound className="mt-0.5 size-5 shrink-0 text-amber-300" />
              <div>
                <p className="text-sm font-bold">Spotify Client ID gerekiyor</p>
                <p className="mt-1 text-xs leading-5 text-muted-foreground"><code>SPOTIFY_CLIENT_ID</code> ortam değişkenini ayarla ve callback olarak <code>http://127.0.0.1:5086/api/auth/spotify/callback</code> ekle.</p>
                <a href="https://developer.spotify.com/dashboard" target="_blank" rel="noreferrer" className="mt-3 inline-flex items-center gap-1 text-xs font-bold text-amber-300 hover:underline">Spotify Dashboard <ExternalLink className="size-3" /></a>
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  )
}

function StatusBadge({ ready, readyLabel, waitingLabel }: { ready: boolean; readyLabel: string; waitingLabel: string }) {
  return <Badge variant={ready ? "success" : "warning"}>{ready ? <CheckCircle2 className="size-3" /> : <XCircle className="size-3" />}{ready ? readyLabel : waitingLabel}</Badge>
}
