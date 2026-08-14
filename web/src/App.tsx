import { useQueryClient } from "@tanstack/react-query"
import { ArrowLeft } from "lucide-react"
import { lazy, Suspense, useEffect } from "react"
import { Link, Route, Routes, useSearchParams } from "react-router-dom"
import { toast } from "sonner"

import { AppShell } from "@/components/app-shell"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"

const HomePage = lazy(() => import("@/pages/home-page").then((module) => ({ default: module.HomePage })))
const HistoryPage = lazy(() => import("@/pages/history-page").then((module) => ({ default: module.HistoryPage })))
const ImportPage = lazy(() => import("@/pages/import-page").then((module) => ({ default: module.ImportPage })))
const SettingsPage = lazy(() => import("@/pages/settings-page").then((module) => ({ default: module.SettingsPage })))

export default function App() {
  return (
    <AppShell>
      <OAuthNotice />
      <Suspense fallback={<PageLoading />}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/imports/:id" element={<ImportPage />} />
          <Route path="/history" element={<HistoryPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </Suspense>
    </AppShell>
  )
}

function OAuthNotice() {
  const [params, setParams] = useSearchParams()
  const queryClient = useQueryClient()
  const result = params.get("spotify")

  useEffect(() => {
    if (!result) return
    if (result === "connected") {
      toast.success("Spotify hesabın bağlandı.")
      void queryClient.invalidateQueries({ queryKey: ["spotify"] })
    } else {
      toast.error("Spotify bağlantısı tamamlanamadı. Tekrar deneyebilirsin.")
    }
    const next = new URLSearchParams(params)
    next.delete("spotify")
    next.delete("reason")
    setParams(next, { replace: true })
  }, [params, queryClient, result, setParams])

  return null
}

function PageLoading() {
  return <div className="grid min-h-80 place-items-center"><span className="size-8 animate-spin rounded-full border-2 border-white/10 border-t-primary" /></div>
}

function NotFound() {
  return (
    <div className="mx-auto max-w-xl py-20">
      <Card>
        <CardContent className="p-10 text-center">
          <p className="text-6xl font-extrabold tracking-[-0.06em] text-primary">404</p>
          <h2 className="mt-4 text-2xl font-extrabold">Bu parça listede yok.</h2>
          <p className="mt-2 text-sm text-muted-foreground">Aradığın sayfa taşınmış veya hiç oluşturulmamış olabilir.</p>
          <Button className="mt-7" asChild><Link to="/"><ArrowLeft /> Ana sayfaya dön</Link></Button>
        </CardContent>
      </Card>
    </div>
  )
}
