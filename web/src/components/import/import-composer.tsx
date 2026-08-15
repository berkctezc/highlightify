import { useGSAP } from "@gsap/react"
import { useMutation, useQuery } from "@tanstack/react-query"
import gsap from "gsap"
import { useRef, useState, type DragEvent, type FormEvent } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"

import { api } from "@/api/client"
import {
  ArrowClockwiseIcon,
  ArrowRightIcon,
  CheckCircleIcon,
  CircleNotchIcon,
  CloudArrowUpIcon,
  FileCodeIcon,
  ImagesIcon,
  LaptopIcon,
  LinkIcon,
  LockKeyIcon,
  MagnifyingGlassIcon,
  MusicNotesIcon,
  PlaylistIcon,
  SpotifyLogoIcon,
  WaveformIcon,
  XIcon,
} from "@/components/icons"
import { SpotifyConnectButton } from "@/components/spotify-connect-button"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Textarea } from "@/components/ui/textarea"
import { useSpotifyConnection } from "@/hooks/use-spotify"
import { cn } from "@/lib/utils"

gsap.registerPlugin(useGSAP)

const workflow = [
  { icon: LinkIcon, title: "Add source", copy: "Story or Highlight link" },
  { icon: WaveformIcon, title: "Verify source", copy: "Select correct track and version" },
  { icon: PlaylistIcon, title: "Send to Playlist", copy: "New or existing playlist" },
]

export function ImportComposer() {
  const pageRef = useRef<HTMLDivElement>(null)
  const fileInput = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const [sourceText, setSourceText] = useState("")
  const [files, setFiles] = useState<File[]>([])
  const [browserSource, setBrowserSource] = useState(() => window.localStorage.getItem("highlightify.browser-source") ?? "")
  const [dragging, setDragging] = useState(false)
  const config = useQuery({ queryKey: ["config"], queryFn: ({ signal }) => api.getConfiguration(signal) })
  const spotify = useSpotifyConnection()
  const startImport = useMutation({
    mutationFn: api.startImport,
    onSuccess: ({ id }) => navigate(`/imports/${id}`),
    onError: (error: Error) => toast.error(error.message),
  })

  const sources = sourceText
    .split(/\r?\n/)
    .map((source) => source.trim())
    .filter(Boolean)
  const sourceCount = sources.length + files.length
  const remaining = Math.max(0, 12 - sourceCount)

  useGSAP(() => {
    const media = gsap.matchMedia()
    media.add("(prefers-reduced-motion: no-preference)", () => {
      gsap.timeline({ defaults: { ease: "power3.out" } })
        .from("[data-intro]", { y: 18, duration: 0.55, stagger: 0.07, clearProps: "transform" })
        .from("[data-composer]", { y: 20, scale: 0.992, duration: 0.58, clearProps: "transform" }, "-=0.3")
        .from("[data-workflow]", { y: 12, duration: 0.4, stagger: 0.06, clearProps: "transform" }, "-=0.28")
    })
    return () => media.revert()
  }, { scope: pageRef })

  function addFiles(nextFiles: File[]) {
    const htmlFiles = nextFiles.filter((file) => /\.html?$/i.test(file.name) && file.size <= 6_000_000)
    if (htmlFiles.length !== nextFiles.length) toast.error("Only HTML files smaller than 6MB allowed.")
    setFiles((current) => [...current, ...htmlFiles]
      .filter((file, index, all) => all.findIndex((candidate) => candidate.name === file.name && candidate.size === file.size) === index)
      .slice(0, 12))
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setDragging(false)
    addFiles(Array.from(event.dataTransfer.files))
  }

  async function pasteFromClipboard() {
    try {
      const clipboard = await navigator.clipboard.readText()
      const nextSources = clipboard.split(/\r?\n/).map((item) => item.trim()).filter(Boolean)
      if (!nextSources.length) {
        toast.info("No compatible link on clipboard.")
        return
      }
      setSourceText((current) => Array.from(new Set([
        ...current.split(/\r?\n/).map((item) => item.trim()).filter(Boolean),
        ...nextSources,
      ])).join("\n"))
      toast.success(nextSources.length === 1 ? "Connection added." : `${nextSources.length} connection added.`)
    } catch {
      toast.error("Cannot access clipboard. Paste link into the field.")
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (sourceCount === 0) {
      toast.error("Add at least one Story or Highlight link.")
      return
    }
    if (sourceCount > 12) {
      toast.error("Only 12 sources could be added at once.")
      return
    }
    startImport.mutate({ sources, files, browserSource: browserSource || config.data?.defaultBrowserSource || "none" })
  }

  function changeBrowserSource(value: string) {
    setBrowserSource(value)
    window.localStorage.setItem("highlightify.browser-source", value)
  }

  return (
    <div ref={pageRef} className="mx-auto max-w-[1180px]">
      <section className="grid gap-7 lg:grid-cols-[minmax(0,1fr)_390px] lg:items-end">
        <div>
          <p data-intro className="type-eyebrow flex items-center gap-2 text-primary"><MusicNotesIcon className="size-4" weight="fill" /> Instagram'dan playlist'e</p>
          <h2 data-intro className="type-display mt-4 max-w-3xl">Duyduğun şarkıyı<br /><span className="text-[#a7a7a7]">orada bırakma.</span></h2>
          <p data-intro className="type-body mt-5 max-w-2xl">Story ve Highlight bağlantılarını ekle. Müziği bulalım; doğru Spotify eşleşmesini sen seç.</p>
        </div>
        <div data-intro className="hidden border-l border-white/10 pl-7 lg:block">
          <p className="text-xs font-extrabold text-white">Kontrol her adımda sende</p>
          <p className="mt-2 text-xs leading-5 text-muted-foreground">Hiçbir parça onayın olmadan playlist'ine eklenmez.</p>
        </div>
      </section>

      <form data-composer onSubmit={submit} className="mt-9 min-w-0 overflow-hidden rounded-[22px] border border-white/[0.07] bg-[#181818] shadow-[0_32px_80px_-48px_rgba(0,0,0,.9)]">
        <div className="grid xl:grid-cols-[minmax(0,1fr)_330px]">
          <section className="min-w-0 p-5 sm:p-7 lg:p-8">
            <div className="flex items-start justify-between gap-5">
              <div>
                <p className="type-eyebrow text-muted-foreground">01 · Kaynak</p>
                <h3 className="type-section-title mt-2">Bağlantılarını ekle</h3>
                <p className="mt-1.5 text-xs leading-5 text-muted-foreground">Her satıra bir Instagram bağlantısı veya Highlight ID'si.</p>
              </div>
              <span className={cn("rounded-full px-3 py-1.5 text-xs font-extrabold", sourceCount > 12 ? "bg-red-400/10 text-red-300" : sourceCount ? "bg-primary/12 text-primary" : "bg-white/[0.06] text-muted-foreground")}>{sourceCount}/12</span>
            </div>

            <div className="mt-6 overflow-hidden rounded-xl border border-white/10 bg-[#0f0f0f] transition focus-within:border-white/25 focus-within:ring-2 focus-within:ring-white/[0.04]">
              <div className="flex min-w-0 items-center justify-between gap-2 border-b border-white/[0.07] px-3 py-2.5 sm:px-4">
                <span className="flex min-w-0 items-center gap-2 truncate text-[11px] font-bold text-muted-foreground"><LinkIcon className="size-3.5 shrink-0" weight="bold" /><span className="hidden sm:inline">Story / Highlight URL</span><span className="sm:hidden">URL / ID</span></span>
                <div className="flex shrink-0 items-center gap-1">
                  {sourceText && <button type="button" className="rounded-full px-2.5 py-1.5 text-[11px] font-bold text-muted-foreground transition hover:bg-white/[0.06] hover:text-white" onClick={() => setSourceText("")}>Temizle</button>}
                  <button type="button" className="rounded-full bg-white px-3 py-1.5 text-[11px] font-extrabold text-black transition hover:scale-[1.03] active:scale-95" onClick={pasteFromClipboard}><span className="hidden sm:inline">Panodan yapıştır</span><span className="sm:hidden">Yapıştır</span></button>
                </div>
              </div>
              <Textarea
                value={sourceText}
                onChange={(event) => setSourceText(event.target.value)}
                placeholder={"https://instagram.com/stories/user/…\nhttps://instagram.com/stories/highlights/…"}
                rows={6}
                className="min-h-44 resize-y rounded-none border-0 bg-transparent px-4 py-4 text-sm leading-7 shadow-none focus:bg-transparent focus:ring-0 sm:text-[15px]"
                aria-label="Instagram Story ve Highlight bağlantıları"
              />
              <div className="flex items-center justify-between border-t border-white/[0.06] px-4 py-2.5 text-[10px] font-semibold text-muted-foreground">
                <span>Story URL · Highlight URL · Highlight ID</span>
                <span>{remaining} yer kaldı</span>
              </div>
            </div>

            <div className="mt-5 grid gap-3 md:grid-cols-2">
              <div className="rounded-xl border border-white/[0.07] bg-[#141414] p-4">
                <div className="mb-3 flex items-center justify-between gap-3">
                  <span className="flex items-center gap-2 text-xs font-extrabold"><LaptopIcon className="size-4 text-muted-foreground" weight="duotone" /> Instagram oturumu</span>
                  <span className="text-[10px] font-bold text-muted-foreground">Özel içerik için</span>
                </div>
                <Select value={browserSource || config.data?.defaultBrowserSource || "none"} onValueChange={changeBrowserSource}>
                  <SelectTrigger aria-label="Instagram browser session"><SelectValue placeholder="Select browser" /></SelectTrigger>
                  <SelectContent><SelectItem value="none">Giriş gerekmiyor</SelectItem><SelectItem value="firefox">Firefox</SelectItem><SelectItem value="brave">Brave</SelectItem><SelectItem value="chrome">Google Chrome</SelectItem><SelectItem value="chromium">Chromium</SelectItem><SelectItem value="edge">Microsoft Edge</SelectItem><SelectItem value="safari">Safari</SelectItem></SelectContent>
                </Select>
                <p className="mt-2.5 flex items-center gap-1.5 text-[10px] text-muted-foreground"><LockKeyIcon className="size-3.5" weight="duotone" /> Çerezlerin bu cihazdan ayrılmaz.</p>
              </div>

              <div
                onDragOver={(event) => { event.preventDefault(); setDragging(true) }}
                onDragLeave={() => setDragging(false)}
                onDrop={handleDrop}
                className={cn("rounded-xl border border-dashed p-4 transition", dragging ? "border-primary bg-primary/[0.06]" : "border-white/[0.12] bg-[#141414]")}
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="flex items-center gap-2 text-xs font-extrabold"><FileCodeIcon className="size-4 text-muted-foreground" weight="duotone" /> HTML yedeği</span>
                  <span className="text-[10px] font-bold text-muted-foreground">İsteğe bağlı</span>
                </div>
                <button type="button" className="mt-3 flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-white/[0.06] text-xs font-extrabold transition hover:bg-white/[0.1]" onClick={() => fileInput.current?.click()}><CloudArrowUpIcon className="size-4" weight="duotone" /> Dosya seç veya sürükle</button>
                <p className="mt-2.5 text-[10px] text-muted-foreground">Instagram veri dışa aktarımındaki HTML dosyaları.</p>
                <input ref={fileInput} className="hidden" type="file" accept=".html,.htm,text/html" multiple onChange={(event) => addFiles(Array.from(event.target.files ?? []))} />
              </div>
            </div>

            {files.length > 0 && (
              <div className="mt-4 flex flex-wrap gap-2">
                {files.map((file, index) => (
                  <span key={`${file.name}-${index}`} className="flex max-w-full items-center gap-2 rounded-full bg-white/[0.07] py-1.5 pl-3 pr-2 text-xs font-semibold">
                    <FileCodeIcon className="size-3.5 text-muted-foreground" weight="duotone" /><span className="max-w-48 truncate">{file.name}</span>
                    <button type="button" className="rounded-full p-0.5 text-muted-foreground hover:bg-white/10 hover:text-white" onClick={() => setFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))} aria-label={`${file.name} dosyasını kaldır`}><XIcon className="size-3.5" weight="bold" /></button>
                  </span>
                ))}
              </div>
            )}
          </section>

          <aside className="min-w-0 border-t border-white/[0.07] bg-[#111111] p-5 sm:p-7 xl:flex xl:flex-col xl:border-l xl:border-t-0">
            <p className="type-eyebrow text-muted-foreground">02 · Hedef</p>
            <h3 className="type-section-title mt-2">Spotify bağlantısı</h3>

            <div className="mt-5">
              {spotify.isPending ? (
                <div className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-[#181818] p-4"><CircleNotchIcon className="size-4 animate-spin text-muted-foreground" weight="bold" /><div><p className="text-xs font-extrabold">Hesabın kontrol ediliyor</p><p className="mt-1 text-[10px] text-muted-foreground">Kayıtlı oturum geri yükleniyor</p></div></div>
              ) : spotify.isError ? (
                <button type="button" onClick={() => spotify.refetch()} className="flex w-full items-center gap-3 rounded-xl border border-white/[0.07] bg-[#181818] p-4 text-left transition hover:bg-[#242424]"><ArrowClockwiseIcon className="size-4" weight="bold" /><div><p className="text-xs font-extrabold">Tekrar kontrol et</p><p className="mt-1 text-[10px] text-muted-foreground">Could not remove existing connection.</p></div></button>
              ) : spotify.data?.connected && spotify.data.profile ? (
                <div className="flex items-center gap-3 rounded-xl border border-[#1ed760]/15 bg-[#1ed760]/[0.055] p-4">
                  <Avatar className="size-10"><AvatarImage src={spotify.data.profile.imageUrl ?? undefined} alt="" /><AvatarFallback>{spotify.data.profile.displayName.slice(0, 2).toUpperCase()}</AvatarFallback></Avatar>
                  <div className="min-w-0 flex-1"><p className="truncate text-xs font-extrabold">{spotify.data.profile.displayName}</p><p className="mt-1 flex items-center gap-1.5 text-[10px] font-bold text-[#1ed760]"><CheckCircleIcon className="size-3" weight="fill" /> Playlist için hazır</p></div>
                  <SpotifyLogoIcon className="size-5 text-white" weight="fill" />
                </div>
              ) : (
                <div className="rounded-xl border border-white/[0.07] bg-black p-4">
                  <SpotifyLogoIcon className="size-6 text-[#1ed760]" weight="fill" />
                  <p className="mt-3 text-xs font-extrabold">Playlist oluşturmak için bağlan</p>
                  <p className="mt-1.5 text-[10px] leading-4 text-muted-foreground">Müzikleri bağlanmadan tarayabilir, hesabını sonuç ekranında da bağlayabilirsin.</p>
                  <SpotifyConnectButton className="mt-4 w-full" size="sm" returnPath="/app">Spotify'a bağlan</SpotifyConnectButton>
                </div>
              )}
            </div>

            <div className="my-6 h-px bg-white/[0.07]" />
            <div className="flex items-end justify-between">
              <div><p className="text-[10px] font-bold text-muted-foreground">İşlenecek kaynak</p><p className="mt-1 text-3xl font-extrabold tracking-[-0.05em]">{sourceCount}</p></div>
              <ImagesIcon className="size-7 text-[#5a5a5a]" weight="duotone" />
            </div>
            <p className="mt-3 text-[11px] leading-5 text-muted-foreground">Önce kaynakları tarayacağız. Bulduğumuz eşleşmeleri sonraki ekranda değiştirebilirsin.</p>

            <div className="mt-auto pt-7">
              <Button size="lg" type="submit" className="w-full justify-between px-5" disabled={startImport.isPending || sourceCount === 0 || sourceCount > 12}>
                <span className="flex items-center gap-2">{startImport.isPending ? <CircleNotchIcon className="animate-spin" weight="bold" /> : <MagnifyingGlassIcon weight="bold" />}{startImport.isPending ? "Taranıyor…" : "Müzikleri tara"}</span><ArrowRightIcon weight="bold" />
              </Button>
              <p className="mt-3 text-center text-[10px] font-semibold text-muted-foreground">Genellikle bir dakikadan kısa sürer</p>
            </div>
          </aside>
        </div>
      </form>

      <section className="mt-10 grid gap-5 border-t border-white/[0.07] pt-7 md:grid-cols-3" aria-label="Aktarım adımları">
        {workflow.map(({ icon: Icon, title, copy }, index) => (
          <div data-workflow key={title} className="flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-white/[0.06] text-muted-foreground"><Icon className="size-4" weight="duotone" /></span>
            <div><p className="text-xs font-extrabold"><span className="mr-1.5 text-muted-foreground">0{index + 1}</span>{title}</p><p className="mt-1 text-[11px] leading-5 text-muted-foreground">{copy}</p></div>
          </div>
        ))}
      </section>

      {config.data && !config.data.ytDlpAvailable && <div className="mt-6 rounded-xl border border-amber-300/15 bg-amber-300/[0.04] px-4 py-3 text-xs leading-5 text-amber-200">Yerel Instagram oturumu için <code className="font-bold">yt-dlp</code> bulunamadı. Herkese açık içerik veya HTML yükleme kullanılabilir.</div>}
    </div>
  )
}