import { useMutation, useQuery } from "@tanstack/react-query"
import { motion } from "motion/react"
import { ArrowRight, FileCode2, GalleryVerticalEnd, LaptopMinimal, Link2, LockKeyhole, Sparkles, UploadCloud, X } from "lucide-react"
import { useRef, useState, type DragEvent, type FormEvent } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"

import { api } from "@/api/client"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Textarea } from "@/components/ui/textarea"
import { useSpotifyActions, useSpotifyConnection } from "@/hooks/use-spotify"
import { cn } from "@/lib/utils"

export function ImportComposer() {
  const navigate = useNavigate()
  const fileInput = useRef<HTMLInputElement>(null)
  const [sourceText, setSourceText] = useState("")
  const [files, setFiles] = useState<File[]>([])
  const [browserSource, setBrowserSource] = useState("none")
  const [dragging, setDragging] = useState(false)
  const config = useQuery({ queryKey: ["config"], queryFn: ({ signal }) => api.getConfiguration(signal) })
  const spotify = useSpotifyConnection()
  const spotifyActions = useSpotifyActions()
  const startImport = useMutation({
    mutationFn: api.startImport,
    onSuccess: ({ id }) => navigate(`/imports/${id}`),
    onError: (error: Error) => toast.error(error.message),
  })

  const sources = sourceText
    .split(/\r?\n/)
    .map((source) => source.trim())
    .filter(Boolean)

  function addFiles(nextFiles: File[]) {
    const htmlFiles = nextFiles.filter((file) => /\.html?$/i.test(file.name))
    if (htmlFiles.length !== nextFiles.length) toast.error("Yalnızca HTML dosyaları eklenebilir.")
    setFiles((current) => [...current, ...htmlFiles].slice(0, 12))
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setDragging(false)
    addFiles(Array.from(event.dataTransfer.files))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (sources.length + files.length === 0) {
      toast.error("En az bir Highlight URL'si veya HTML dosyası ekleyin.")
      return
    }
    startImport.mutate({ sources, files, browserSource })
  }

  return (
    <div className="grid gap-7 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,.65fr)]">
      <section>
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.45 }}
          className="mb-8 max-w-3xl"
        >
          <Badge><Sparkles className="size-3" /> Instagram'dan Spotify'a</Badge>
          <h2 className="text-balance mt-5 text-[clamp(2.25rem,6vw,4.85rem)] font-extrabold leading-[.96] tracking-[-0.065em]">
            Anıların bir<br />
            <span className="text-primary">playlist'e dönüşsün.</span>
          </h2>
          <p className="mt-5 max-w-xl text-base leading-7 text-muted-foreground sm:text-lg">
            Highlight bağlantılarını ekle. Müziği bulalım, Spotify eşleşmelerini senin kontrolüne bırakalım.
          </p>
        </motion.div>

        <motion.form
          onSubmit={submit}
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.45, delay: 0.08 }}
        >
          <Card className="overflow-hidden bg-[#111411]/92">
            <CardContent className="p-0">
              <div className="border-b border-white/7 p-5 sm:p-7">
                <div className="mb-4 flex items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2 text-sm font-extrabold"><GalleryVerticalEnd className="size-4 text-accent" /> Highlight kaynakları</div>
                    <p className="mt-1.5 text-sm text-muted-foreground">Her satıra bir bağlantı veya Highlight ID'si yaz.</p>
                  </div>
                  <span className="rounded-full bg-white/5 px-2.5 py-1 text-[11px] font-bold text-muted-foreground">En fazla 12</span>
                </div>
                <Textarea
                  value={sourceText}
                  onChange={(event) => setSourceText(event.target.value)}
                  placeholder={"https://instagram.com/stories/highlights/…\n17876436264678750"}
                  rows={4}
                  aria-label="Instagram Highlight bağlantıları"
                />
              </div>

              <div className="grid sm:grid-cols-2">
                <div className="border-b border-white/7 p-5 sm:border-r sm:border-b-0 sm:p-7">
                  <div className="mb-4 flex items-center gap-2 text-sm font-extrabold"><LaptopMinimal className="size-4 text-primary" /> Yerel oturum</div>
                  <Select value={browserSource} onValueChange={setBrowserSource}>
                    <SelectTrigger aria-label="Instagram tarayıcı oturumu">
                      <SelectValue placeholder="Tarayıcı seç" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Giriş gerekmiyor</SelectItem>
                      <SelectItem value="firefox">Firefox</SelectItem>
                      <SelectItem value="chrome">Google Chrome</SelectItem>
                      <SelectItem value="chromium">Chromium</SelectItem>
                      <SelectItem value="edge">Microsoft Edge</SelectItem>
                      <SelectItem value="safari">Safari</SelectItem>
                    </SelectContent>
                  </Select>
                  <div className="mt-3 flex items-start gap-2 text-xs leading-5 text-muted-foreground">
                    <LockKeyhole className="mt-0.5 size-3.5 shrink-0" />
                    Çerezler cihazından ayrılmaz; işlem yerel serviste yapılır.
                  </div>
                </div>

                <div className="p-5 sm:p-7">
                  <div className="mb-4 flex items-center gap-2 text-sm font-extrabold"><FileCode2 className="size-4 text-accent" /> HTML yedeği</div>
                  <div
                    role="button"
                    tabIndex={0}
                    onClick={() => fileInput.current?.click()}
                    onKeyDown={(event) => event.key === "Enter" && fileInput.current?.click()}
                    onDragOver={(event) => { event.preventDefault(); setDragging(true) }}
                    onDragLeave={() => setDragging(false)}
                    onDrop={handleDrop}
                    className={cn(
                      "flex min-h-24 cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed px-3 text-center transition",
                      dragging ? "border-primary bg-primary/8" : "border-white/13 bg-white/3 hover:border-white/25 hover:bg-white/5",
                    )}
                  >
                    <UploadCloud className="mb-2 size-5 text-muted-foreground" />
                    <span className="text-xs font-bold">Sürükle veya dosya seç</span>
                    <span className="mt-1 text-[10px] text-muted-foreground">HTML · dosya başına 6 MB</span>
                  </div>
                  <input
                    ref={fileInput}
                    className="hidden"
                    type="file"
                    accept=".html,.htm,text/html"
                    multiple
                    onChange={(event) => addFiles(Array.from(event.target.files ?? []))}
                  />
                </div>
              </div>

              {files.length > 0 && (
                <div className="flex flex-wrap gap-2 border-t border-white/7 px-5 py-4 sm:px-7">
                  {files.map((file, index) => (
                    <span key={`${file.name}-${index}`} className="flex max-w-full items-center gap-2 rounded-full bg-white/6 py-1.5 pr-2 pl-3 text-xs font-semibold">
                      <FileCode2 className="size-3.5 text-muted-foreground" />
                      <span className="max-w-48 truncate">{file.name}</span>
                      <button type="button" className="rounded-full p-0.5 hover:bg-white/10" onClick={() => setFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))} aria-label={`${file.name} dosyasını kaldır`}>
                        <X className="size-3.5" />
                      </button>
                    </span>
                  ))}
                </div>
              )}

              <div className="flex flex-col gap-4 border-t border-white/7 bg-black/10 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-7">
                <div className="flex items-center gap-3 text-sm text-muted-foreground">
                  <span className="grid size-9 place-items-center rounded-full bg-white/6"><Link2 className="size-4" /></span>
                  <span><strong className="text-foreground">{sources.length + files.length}</strong> kaynak hazır</span>
                </div>
                <Button size="lg" type="submit" disabled={startImport.isPending}>
                  {startImport.isPending ? "Başlatılıyor…" : "Müzikleri bul"}<ArrowRight />
                </Button>
              </div>
            </CardContent>
          </Card>
        </motion.form>
      </section>

      <aside className="space-y-4 xl:pt-22">
        <Card className="glass-panel overflow-hidden">
          <CardContent className="p-6">
            <div className="mb-7 flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-[0.13em] text-muted-foreground">Hazırlık</span>
              <span className={cn("size-2 rounded-full", spotify.data?.connected ? "bg-emerald-400 shadow-[0_0_14px_#34d399]" : "bg-amber-300")} />
            </div>
            <h3 className="text-xl font-extrabold tracking-[-0.035em]">
              {spotify.data?.connected ? "Spotify hazır." : "Önce Spotify'ı bağla."}
            </h3>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              {spotify.data?.connected
                ? `${spotify.data.profile?.displayName ?? "Hesabın"} için eşleşmeleri ve playlistlerini getirebiliriz.`
                : "Şarkıları bulduktan sonra eşleştirmek ve playlist'e eklemek için bağlantı gerekiyor."}
            </p>
            {!spotify.data?.connected && (
              <Button className="mt-5 w-full" variant="secondary" onClick={() => spotifyActions.connect("/")} disabled={!spotify.data?.configured}>
                Spotify'a bağlan
              </Button>
            )}
            {spotify.data && !spotify.data.configured && (
              <p className="mt-3 text-xs leading-5 text-amber-300">Sunucuda SPOTIFY_CLIENT_ID henüz ayarlanmamış.</p>
            )}
          </CardContent>
        </Card>

        <Card className="border-accent/12 bg-accent/[0.045]">
          <CardContent className="p-6">
            <p className="text-xs font-bold uppercase tracking-[0.13em] text-accent">Nasıl çalışır?</p>
            <ol className="mt-5 space-y-5">
              {["Highlight verisini güvenle tara", "Spotify eşleşmelerini kontrol et", "Yeni veya mevcut playlist'e ekle"].map((label, index) => (
                <li key={label} className="flex items-center gap-3 text-sm font-semibold">
                  <span className="grid size-7 shrink-0 place-items-center rounded-full border border-white/10 bg-black/15 text-[11px] font-extrabold text-muted-foreground">0{index + 1}</span>
                  {label}
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>

        {config.data && !config.data.ytDlpAvailable && (
          <div className="rounded-2xl border border-amber-300/15 bg-amber-300/6 p-4 text-xs leading-5 text-amber-200">
            Yerel Instagram oturumu için <code className="font-bold">yt-dlp</code> bulunamadı. Herkese açık Highlight veya HTML yükleme yine kullanılabilir.
          </div>
        )}
      </aside>
    </div>
  )
}
