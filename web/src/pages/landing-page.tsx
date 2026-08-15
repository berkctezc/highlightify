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
  { icon: LinkIcon, number: "01", title: "Add links", copy: "Add Story or Highlight URLs all at once." },
  { icon: WaveformIcon, number: "02", title: "Review matches", copy: "Compare the Spotify versions we found and confirm the right one." },
  { icon: PlaylistIcon, number: "03", title: "Export playlist", copy: "Add your selections to a new or existing playlist." },
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
          <a href="#how-it-works" className="hidden rounded-full px-4 py-2 text-xs font-bold text-muted-foreground transition hover:text-white sm:inline-flex">How it works</a>
          <Button size="sm" asChild><Link to="/app">Launch app <ArrowRightIcon weight="bold" /></Link></Button>
        </div>
      </header>

      <main>
        <section className="relative mx-auto grid min-h-[calc(100svh-5rem)] w-full max-w-[1280px] items-center gap-8 px-5 pb-14 pt-8 sm:px-8 lg:grid-cols-[minmax(0,1fr)_minmax(360px,.72fr)] lg:gap-12 lg:pb-20 lg:pt-4">
          <div className="relative z-10 max-w-3xl">
            <p data-landing-copy className="type-eyebrow flex items-center gap-2 text-primary"><span className="size-1.5 rounded-full bg-current shadow-[0_0_16px_currentColor]" /> Your Instagram Music Archive</p>
            <h1 data-landing-copy className="type-landing-display mt-5">Stories are temporary.<br /><span className="text-[#a7a7a7]">Let the music live.</span></h1>
            <p data-landing-copy className="type-body mt-6 max-w-xl">Find tracks from Stories and Highlights, choose the right versions, and save them to your own Spotify playlist.</p>
            <div data-landing-copy className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button size="lg" asChild><Link to="/app">Find my tracks <ArrowRightIcon weight="bold" /></Link></Button>
              <Button size="lg" variant="outline" asChild><a href="#how-it-works">See how it works</a></Button>
            </div>
            <div data-landing-copy className="mt-8 flex flex-wrap gap-x-5 gap-y-2 text-[11px] font-bold text-muted-foreground">
              <span className="flex items-center gap-1.5"><CheckCircleIcon className="size-3.5 text-primary" weight="fill" /> You stay in control</span>
              <span className="flex items-center gap-1.5"><ShieldCheckIcon className="size-3.5 text-primary" weight="fill" /> Runs locally and is safe</span>
              <span className="flex items-center gap-1.5"><CheckCircleIcon className="size-3.5 text-primary" weight="fill" /> No repeated sign-ins required</span>
            </div>
          </div>

          <div data-landing-art className="relative mx-auto w-full max-w-[520px] lg:mx-0">
            <div className="pointer-events-none absolute inset-[12%] rounded-full bg-[radial-gradient(circle,rgba(180,155,200,.22),rgba(30,215,96,.08)_44%,transparent_70%)] blur-3xl" />
            <div data-landing-art-inner className="landing-art-mask relative">
              <img src="/highlightify-landing-hero.webp" width="960" height="1200" alt="" className="h-auto w-full select-none" fetchPriority="high" />
            </div>
          </div>
        </section>

        <section id="how-it-works" className="scroll-mt-12 border-t border-white/[0.07] bg-[#0d0d0d] py-24 sm:py-28">
          <div className="mx-auto w-full max-w-[1180px] px-5 sm:px-8">
            <div className="max-w-2xl">
              <p className="type-eyebrow text-primary">How it works</p>
              <h2 className="type-page-title mt-4">Three steps. One playlist.</h2>
              <p className="type-body mt-4">Technical details stay in the background; choice and result stay with you.</p>
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
            <div><p className="type-eyebrow text-primary">Ready to start?</p><h2 className="type-page-title mt-3 max-w-2xl">Your first playlist is one link away.</h2></div>
            <Button size="lg" asChild><Link to="/app">Go to the app <ArrowRightIcon weight="bold" /></Link></Button>
          </div>
        </section>
      </main>

      <footer className="border-t border-white/[0.07] bg-black">
        <div className="mx-auto flex max-w-[1280px] flex-col gap-3 px-5 py-7 text-[10px] font-semibold text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-8"><BrandMark /><p>Highlightify is an independent product. Spotify access is only granted with your approval.</p></div>
      </footer>
    </div>
  )
}
