import { useGSAP } from "@gsap/react"
import gsap from "gsap"
import { ScrollTrigger } from "gsap/ScrollTrigger"
import { useRef } from "react"
import { Link } from "react-router-dom"

import { BrandMark } from "@/components/brand-mark"
import {
  ArrowRightIcon,
  CheckCircleIcon,
  LinkIcon,
  PlaylistIcon,
  ShieldCheckIcon,
  WaveformIcon,
} from "@/components/icons"
import { Button } from "@/components/ui/button"

gsap.registerPlugin(useGSAP, ScrollTrigger)

const steps = [
  { icon: LinkIcon, number: "01", title: "Bağlantıyı bırak", copy: "Story veya Highlight URL'lerini tek seferde ekle." },
  { icon: WaveformIcon, number: "02", title: "Doğru parçayı seç", copy: "Bulunan Spotify sürümlerini karşılaştır ve sonucu doğrula." },
  { icon: PlaylistIcon, number: "03", title: "Playlist'e gönder", copy: "Seçtiklerini yeni ya da mevcut listene ekle." },
]

export function LandingPage() {
  const pageRef = useRef<HTMLDivElement>(null)

  useGSAP(() => {
    const media = gsap.matchMedia()
    media.add("(prefers-reduced-motion: no-preference)", () => {
      gsap.timeline({ defaults: { ease: "power3.out", clearProps: "transform" } })
        .from("[data-landing-nav]", { y: -16, duration: 0.5 })
        .from("[data-landing-copy]", { y: 24, duration: 0.62, stagger: 0.08 }, "-=0.22")
        .from("[data-landing-art]", { x: 28, scale: 0.97, duration: 0.72 }, "-=0.48")

      gsap.to("[data-landing-art-inner]", {
        y: -10,
        rotation: 0.35,
        duration: 4.2,
        ease: "sine.inOut",
        repeat: -1,
        yoyo: true,
      })

      gsap.from("[data-step]", {
        y: 24,
        duration: 0.55,
        stagger: 0.1,
        ease: "power3.out",
        clearProps: "transform",
        scrollTrigger: { trigger: "[data-steps]", start: "top 78%", once: true },
      })

      gsap.from("[data-final-cta]", {
        y: 26,
        scale: 0.985,
        duration: 0.65,
        ease: "power3.out",
        clearProps: "transform",
        scrollTrigger: { trigger: "[data-final-cta]", start: "top 82%", once: true },
      })
    })
    return () => media.revert()
  }, { scope: pageRef })

  return (
    <div ref={pageRef} className="min-h-screen overflow-hidden bg-black text-white">
      <header data-landing-nav className="relative z-30 mx-auto flex h-20 w-full max-w-[1280px] items-center justify-between px-5 sm:px-8">
        <BrandMark />
        <div className="flex items-center gap-2 sm:gap-3">
          <a href="#nasil-calisir" className="hidden rounded-full px-4 py-2 text-xs font-bold text-muted-foreground transition hover:text-white sm:inline-flex">Nasıl çalışır?</a>
          <Button size="sm" asChild><Link to="/app">Uygulamayı aç <ArrowRightIcon weight="bold" /></Link></Button>
        </div>
      </header>

      <main>
        <section className="relative mx-auto grid min-h-[calc(100svh-5rem)] w-full max-w-[1280px] items-center gap-8 px-5 pb-14 pt-8 sm:px-8 lg:grid-cols-[minmax(0,1fr)_minmax(360px,.72fr)] lg:gap-12 lg:pb-20 lg:pt-4">
          <div className="relative z-10 max-w-3xl">
            <p data-landing-copy className="type-eyebrow flex items-center gap-2 text-primary"><span className="size-1.5 rounded-full bg-current shadow-[0_0_16px_currentColor]" /> Instagram müzik arşivin</p>
            <h1 data-landing-copy className="type-landing-display mt-5">Hikâyeler geçer.<br /><span className="text-[#a7a7a7]">Müzikleri kalsın.</span></h1>
            <p data-landing-copy className="type-body mt-6 max-w-xl">Story ve Highlight'larda keşfettiğin parçaları bul, doğru sürümü seç ve kendi Spotify playlist'ine taşı.</p>
            <div data-landing-copy className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button size="lg" asChild><Link to="/app">Müziklerimi bul <ArrowRightIcon weight="bold" /></Link></Button>
              <Button size="lg" variant="outline" asChild><a href="#nasil-calisir">Nasıl çalıştığını gör</a></Button>
            </div>
            <div data-landing-copy className="mt-8 flex flex-wrap gap-x-5 gap-y-2 text-[11px] font-bold text-muted-foreground">
              <span className="flex items-center gap-1.5"><CheckCircleIcon className="size-3.5 text-primary" weight="fill" /> Son kontrol sende</span>
              <span className="flex items-center gap-1.5"><ShieldCheckIcon className="size-3.5 text-primary" weight="fill" /> Yerel ve güvenli</span>
              <span className="flex items-center gap-1.5"><CheckCircleIcon className="size-3.5 text-primary" weight="fill" /> Tekrar giriş istemez</span>
            </div>
          </div>

          <div data-landing-art className="relative mx-auto w-full max-w-[520px] lg:mx-0">
            <div className="pointer-events-none absolute inset-[12%] rounded-full bg-[radial-gradient(circle,rgba(180,155,200,.22),rgba(30,215,96,.08)_44%,transparent_70%)] blur-3xl" />
            <div data-landing-art-inner className="landing-art-mask relative">
              <img src="/highlightify-landing-hero.webp" width="960" height="1200" alt="" className="h-auto w-full select-none" fetchPriority="high" />
            </div>
          </div>
        </section>

        <section id="nasil-calisir" className="scroll-mt-12 border-t border-white/[0.07] bg-[#0d0d0d] py-24 sm:py-28">
          <div className="mx-auto w-full max-w-[1180px] px-5 sm:px-8">
            <div className="max-w-2xl">
              <p className="type-eyebrow text-primary">Nasıl çalışır?</p>
              <h2 className="type-page-title mt-4">Üç adım. Tek bir playlist.</h2>
              <p className="type-body mt-4">Teknik ayrıntılar arkada kalır; seçim ve sonuç sende kalır.</p>
            </div>

            <div data-steps className="mt-12 grid border-y border-white/[0.08] md:grid-cols-3">
              {steps.map(({ icon: Icon, number, title, copy }, index) => (
                <article data-step key={title} className="border-b border-white/[0.08] py-7 md:border-b-0 md:px-7 md:py-9 md:first:pl-0 md:last:pr-0 [&:not(:last-child)]:md:border-r">
                  <div className="flex items-center justify-between"><span className="grid size-11 place-items-center rounded-full bg-white/[0.06] text-white"><Icon className="size-5" weight="duotone" /></span><span className="type-eyebrow text-[#5f5f5f]">{number}</span></div>
                  <h3 className="type-section-title mt-7">{title}</h3>
                  <p className="mt-2 max-w-xs text-xs leading-5 text-muted-foreground">{copy}</p>
                  {index < steps.length - 1 && <ArrowRightIcon className="mt-6 hidden size-4 text-[#505050] md:block" weight="bold" />}
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="bg-[#0d0d0d] px-5 pb-24 pt-8 sm:px-8 sm:pb-28">
          <div data-final-cta className="mx-auto flex max-w-[1180px] flex-col items-start justify-between gap-8 overflow-hidden rounded-[24px] border border-primary/15 bg-[radial-gradient(circle_at_82%_10%,rgba(30,215,96,.17),transparent_35%),#181818] p-7 sm:p-10 lg:flex-row lg:items-center lg:p-12">
            <div><p className="type-eyebrow text-primary">Hazırsan başlayalım</p><h2 className="type-page-title mt-3 max-w-2xl">İlk playlist'in bir bağlantı kadar yakın.</h2></div>
            <Button size="lg" asChild><Link to="/app">Sisteme geç <ArrowRightIcon weight="bold" /></Link></Button>
          </div>
        </section>
      </main>

      <footer className="border-t border-white/[0.07] bg-black">
        <div className="mx-auto flex max-w-[1280px] flex-col gap-3 px-5 py-7 text-[10px] font-semibold text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-8"><BrandMark /><p>Highlightify bağımsız bir üründür. Spotify ile bağlantı yalnızca kullanıcı onayıyla kurulur.</p></div>
      </footer>
    </div>
  )
}
