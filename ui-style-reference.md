# UI Style Reference



This document is a visual style reference for the Module 2 frontend MVP polish phase.



It must not override:

- product requirements in SPEC.md

- authentication behavior

- database schema

- existing routes

- existing assessment/submission/reporting functionality



The original visual inspiration referenced an example brand name. Do not use that brand name in the project UI unless the team explicitly chooses it.  

Project stack decision for documentation consistency:

- Preferred stack for this project is Next.js App Router + TypeScript + Tailwind CSS.
- Do not convert a Next.js app to Vite.
- Do not add `react-router-dom` to a Next.js app.
- Treat any Vite-specific wording from the original visual prompt as non-binding inspiration only.

Use only the visual style ideas:

- dark premium layout with deep navy/purple gradients

- glassmorphism / liquid glass panels

- neon cyan and purple accent highlights

- editorial typography

- cinematic dashboard sections

- Framer Motion transitions

- polished dashboard cards with glowing borders



Do not copy unrelated agency content, fake testimonials, fake partner names, or unrelated marketing claims.



Build dark, premium dashboard and IDE screens for an AI-powered coding assessment platform using the project frontend stack. For the preferred structure, this means Next.js App Router + TypeScript + Tailwind CSS + shadcn/ui-compatible components + Framer Motion/motion if already installed or approved. The page has a luxury editorial aesthetic -- deep navy/purple backgrounds, white text, neon glow (glassmorphism) effects, and animated particle/grid backgrounds.



FONTS

Import from Google Fonts:



https://fonts.googleapis.com/css2?family=Instrument+Serif:ital@0;1\&family=Barlow:wght@300;400;500;600\&family=JetBrains+Mono:wght@400;500\&display=swap

* Headings: Instrument Serif (italic) -- used via Tailwind class font-heading

* Body: Barlow (weights 300, 400, 500, 600) -- used via Tailwind class font-body

* Code/Mono: JetBrains Mono (weights 400, 500) -- used via Tailwind class font-mono

Tailwind config extends fontFamily:



heading: \["'Instrument Serif'", "serif"]

body: \["'Barlow'", "sans-serif"]

mono: \["'JetBrains Mono'", "monospace"]



COLOR THEME (CSS custom properties, HSL format)



:root {

&#x20; --background: 230 30% 8%;

&#x20; --background-secondary: 240 25% 11%;

&#x20; --foreground: 0 0% 100%;

&#x20; --card: 235 28% 13%;

&#x20; --card-foreground: 0 0% 100%;

&#x20; --primary: 185 100% 60%;

&#x20; --primary-foreground: 230 30% 8%;

&#x20; --secondary: 270 60% 60%;

&#x20; --secondary-foreground: 0 0% 100%;

&#x20; --muted: 230 20% 20%;

&#x20; --muted-foreground: 0 0% 100% / 0.5;

&#x20; --accent: 185 100% 60%;

&#x20; --accent-foreground: 230 30% 8%;

&#x20; --destructive: 0 84.2% 60.2%;

&#x20; --border: 0 0% 100% / 0.08;

&#x20; --input: 0 0% 100% / 0.1;

&#x20; --ring: 185 100% 60% / 0.4;

&#x20; --radius: 0.75rem;

&#x20; --neon-cyan: #00e5ff;

&#x20; --neon-purple: #a855f7;

&#x20; --neon-pink: #ec4899;

&#x20; --glass-bg: rgba(255, 255, 255, 0.04);

&#x20; --glass-border: rgba(255, 255, 255, 0.08);

&#x20; --glass-shadow: 0 4px 30px rgba(0, 0, 0, 0.4);

&#x20; --glass-blur: 20px;

&#x20; --glow-cyan: 0 0 20px rgba(0, 229, 255, 0.3);

&#x20; --glow-purple: 0 0 20px rgba(168, 85, 247, 0.3);

}



LIQUID GLASS CSS (the core visual effect)

Two utility classes defined in the project global stylesheet under @layer components, for example `src/app/globals.css` in a Next.js App Router app:



.liquid-glass (subtle panel):



.liquid-glass {

&#x20; background: rgba(255, 255, 255, 0.04);

&#x20; background-blend-mode: luminosity;

&#x20; backdrop-filter: blur(12px);

&#x20; -webkit-backdrop-filter: blur(12px);

&#x20; border: 1px solid rgba(255, 255, 255, 0.08);

&#x20; box-shadow: 0 4px 30px rgba(0, 0, 0, 0.3), inset 0 1px 1px rgba(255, 255, 255, 0.06);

&#x20; position: relative;

&#x20; overflow: hidden;

}

.liquid-glass::before {

&#x20; content: '';

&#x20; position: absolute;

&#x20; inset: 0;

&#x20; border-radius: inherit;

&#x20; padding: 1px;

&#x20; background: linear-gradient(

&#x20;   180deg,

&#x20;   rgba(0, 229, 255, 0.2) 0%,

&#x20;   rgba(255, 255, 255, 0.05) 30%,

&#x20;   rgba(255, 255, 255, 0) 60%,

&#x20;   rgba(168, 85, 247, 0.15) 100%

&#x20; );

&#x20; -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);

&#x20; -webkit-mask-composite: xor;

&#x20; mask-composite: exclude;

&#x20; pointer-events: none;

}



.liquid-glass-neon (glowing border, used on IDE/featured panels):



.liquid-glass-neon {

&#x20; background: rgba(0, 229, 255, 0.03);

&#x20; backdrop-filter: blur(20px);

&#x20; -webkit-backdrop-filter: blur(20px);

&#x20; border: none;

&#x20; box-shadow: 0 0 0 1.5px rgba(0, 229, 255, 0.6),

&#x20;   0 0 30px rgba(0, 229, 255, 0.15),

&#x20;   inset 0 1px 1px rgba(255, 255, 255, 0.08);

&#x20; position: relative;

&#x20; overflow: hidden;

}

.liquid-glass-neon::before {

&#x20; content: '';

&#x20; position: absolute;

&#x20; inset: 0;

&#x20; border-radius: inherit;

&#x20; padding: 1.5px;

&#x20; background: linear-gradient(

&#x20;   135deg,

&#x20;   rgba(0, 229, 255, 0.8) 0%,

&#x20;   rgba(168, 85, 247, 0.6) 50%,

&#x20;   rgba(0, 229, 255, 0.8) 100%

&#x20; );

&#x20; -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);

&#x20; -webkit-mask-composite: xor;

&#x20; mask-composite: exclude;

&#x20; pointer-events: none;

&#x20; animation: borderGlow 3s ease-in-out infinite;

}

@keyframes borderGlow {

&#x20; 0%, 100% { opacity: 1; }

&#x20; 50% { opacity: 0.6; }

}



The ::before pseudo-element creates a gradient border effect using the mask-composite trick (neon glowing border that pulses subtly).



BACKGROUND PATTERN

Animated dot grid background using CSS + SVG:



.bg-grid {

&#x20; background-image:

&#x20;   radial-gradient(ellipse 80% 50% at 20% 40%, rgba(0, 229, 255, 0.08) 0%, transparent 60%),

&#x20;   radial-gradient(ellipse 60% 40% at 80% 70%, rgba(168, 85, 247, 0.1) 0%, transparent 60%),

&#x20;   radial-gradient(circle at 1px 1px, rgba(255,255,255,0.06) 1px, transparent 0);

&#x20; background-size: auto, auto, 28px 28px;

}



Scattered animated particles (floating cyan/purple dots, 6â€“10 total) positioned absolutely across the background using motion.div with animate={{ y: \[0, -20, 0], opacity: \[0.3, 0.8, 0.3] }} loops.



LAYOUT STRUCTURE

Three-column dashboard layout:



LEFT SIDEBAR (fixed, w-64):

&#x20; \[Logo + Brand]

&#x20; \[Nav Items]

&#x20; \[Bottom Settings]



CENTER MAIN (flex-1, overflow-y-auto):

&#x20; \[Top Search Bar]

&#x20; \[AI IDE Panel]

&#x20; \[Recent Assessments Table]

&#x20; \[Bottom Cards Row]



RIGHT PANEL (fixed, w-80):

&#x20; \[Performance Analytics Chart]

&#x20; \[Language Proficiency Chart]

&#x20; \[Team Activity Feed]



SECTION-BY-SECTION BREAKDOWN



1\. LEFT SIDEBAR

* Background: liquid-glass border-r border-white/5, full height, flex col

* Top: Logo mark (simple cyan code/assessment icon, h-8 w-8) + project brand text such as "AI Coding Assessment". Do not use the AetherFlow name unless the team explicitly chooses it.

* Nav items (mt-10, flex col gap-1):

&#x20;   \* Icon + label layout, px-4 py-2.5 rounded-xl font-body text-sm

&#x20;   \* Items: Dashboard (grid icon), Code Editor (</> icon, ACTIVE STATE: bg-white/8 text-cyan-400 border border-white/10), Analytics (bar chart icon), Assessments (users icon), Settings (gear icon)

&#x20;   \* Active item left border accent: border-l-2 border-cyan-400

* Bottom (mt-auto): Settings link with gear icon, text-white/40 text-xs



2\. TOP SEARCH BAR

* Full-width bar: liquid-glass rounded-2xl px-4 py-3, flex items-center gap-3

* Search icon (text-white/30) + input placeholder "Search..." font-body text-white/50 text-sm bg-transparent

* Right side: small grid of colored dot indicators (status lights, 4 cyan dots arranged 2x2)

* Right: User avatar (rounded-full h-9 w-9) + dropdown chevron



3\. AI IDE PANEL (hero panel, center-left)

* Label: "AI Coding Workspace" or the current assessment title -- font-body font-semibold text-white text-base

* Badge pill top-right: assessment attempt status such as "Active" or "Mock attempt" liquid-glass rounded-full px-3 py-1 text-xs text-white/70 + overflow menu

* Container: liquid-glass-neon rounded-2xl p-4, two-column layout (70% code / 30% AI chat)

* LEFT: Code editor area

&#x20;   \* Dark inner bg: bg-black/30 rounded-xl p-4 font-mono text-sm

&#x20;   \* Line numbers: text-white/20 text-right pr-3 select-none

&#x20;   \* Code syntax coloring:

&#x20;       \* Keywords (def, for, if, return, in): text-purple-400

&#x20;       \* Functions/identifiers: text-cyan-300

&#x20;       \* Strings/values: text-orange-300

&#x20;       \* Default: text-white/80

&#x20;   \* Animated cursor blinking in active line

* RIGHT: AI Assistant panel

&#x20;   \* Header: brain/sparkle icon (cyan) + "AI assistant" text-white font-body font-medium text-sm + â€¢â€¢â€¢ menu

&#x20;   \* Message bubble: liquid-glass rounded-xl p-3 text-white/80 font-body font-light text-xs leading-relaxed

&#x20;   \* Input bar at bottom: liquid-glass rounded-xl px-3 py-2.5 flex items-center, placeholder "Your assistant..." text-white/30 text-xs + send button (cyan arrow icon)

* Top/bottom decorative dots: scattered pixel-art style colored squares (cyan, purple, white) positioned absolute around the panel border



4\. RECENT ASSESSMENTS TABLE

* Header: "Recent Coding Assessments" font-body font-semibold text-white text-base + status/filter badge + overflow menu

* Container: liquid-glass rounded-2xl overflow-hidden

* Table header row: bg-transparent, columns: Name / Availability / Session Status / Progress / Completed -- text-white/40 text-xs font-body font-medium uppercase tracking-wider

* Data rows: hover:bg-white/3 transition

&#x20;   \* Name: text-white font-body text-sm

&#x20;   \* Status (Complete): text-purple-400 font-body text-sm

&#x20;   \* Status 2 (Cakichy): text-cyan-400 font-body text-sm

&#x20;   \* Progress (63%): text-cyan-400 font-body font-medium text-sm

&#x20;   \* Completed: text-white/40 font-body text-sm



5\. BOTTOM CARDS ROW (3-column grid)

* Col 1: Upcoming Deadlines card

&#x20;   \* Header: "Upcoming Deadlines" + â€¢â€¢â€¢ menu

&#x20;   \* Items: each has left purple accent bar (w-1 h-full bg-purple-500 rounded-full), title text-white text-sm font-body, subtitle text-white/50 text-xs, date right-aligned text-white/70 text-sm font-body

&#x20;   \* Example items must use relevant assessment content such as "Python Basics closes today" or "JavaScript Arrays due Jun 7"

* Col 2: Notifications/Activity card

&#x20;   \* Items: purple left-accent bar, title text-white/80 text-sm, description text-white/40 text-xs, date text-white/50 text-sm

* Col 3 (right panel): Team Activity Feed

&#x20;   \* Header: "Team Activity Feed" + â€¢â€¢â€¢ menu + sparkle/star icon (bottom right, decorative)

&#x20;   \* Items: avatar circle (h-8 w-8 rounded-full) + name text-cyan-400 font-body text-sm + action text text-white/60 text-xs + time ago text-white/30 text-xs



6\. RIGHT PANEL â€” PERFORMANCE ANALYTICS

* Header: "Performance Analytics" font-body font-semibold text-white text-base + â€¢â€¢â€¢ menu

* Legend: "Candidate Score" (pink/magenta dot) + "Time Complexity" (cyan dot) -- text-white/50 text-xs font-body

* Chart: smooth area/line chart (recharts AreaChart), dual lines

&#x20;   \* Line 1 (Candidate Score): stroke="#ec4899", fill gradient pink 0.2 -> transparent

&#x20;   \* Line 2 (Time Complexity): stroke="#00e5ff", fill gradient cyan 0.15 -> transparent

&#x20;   \* X axis labels: Jan Feb Mar Apr May -- text-white/30 text-xs

&#x20;   \* Y axis: 0 25 50 75 100 -- text-white/30 text-xs

&#x20;   \* Grid lines: stroke="rgba(255,255,255,0.05)"

&#x20;   \* Tooltip: liquid-glass rounded-lg px-3 py-2 text-xs



7\. RIGHT PANEL â€” LANGUAGE PROFICIENCY

* Header: "Language Proficiency" font-body font-semibold text-white text-base

* Chart: vertical bar chart (recharts BarChart), 3 bars

&#x20;   \* Python: \~55 height, fill gradient purple-to-cyan

&#x20;   \* JS: \~80 height, fill gradient purple-to-cyan

&#x20;   \* Java: \~68 height, fill gradient purple-to-cyan

&#x20;   \* Bar fill: linearGradient from #a855f7 (top) to #00e5ff (bottom), rounded top corners (radius 4)

&#x20;   \* X axis: Python / JavaScript only for the first student MVP -- text-white/40 text-xs font-body

&#x20;   \* Y axis: 0 25 50 75 100 -- text-white/30 text-xs

&#x20;   \* Grid lines: stroke="rgba(255,255,255,0.05)"



KEY DEPENDENCIES

Use only dependencies that fit the existing project stack. For the preferred Next.js App Router frontend:

- `lucide-react` for icons
- `motion` or `framer-motion` only if animation polish is in scope and the dependency is already available or approved
- `recharts` only if chart components are needed for admin/report visuals and the dependency is already available or approved
- Do not add `react-router-dom` to a Next.js project

Icons used from lucide-react: LayoutDashboard, Code2, BarChart3, Users, Settings, Search, Brain, Send, ChevronDown, MoreHorizontal, Sparkles



OVERALL PAGE STRUCTURE



<div className="bg-grid min-h-screen bg-\[hsl(var(--background))] flex overflow-hidden">

&#x20; {/\* Ambient particles \*/}

&#x20; <ParticleBackground />



&#x20; {/\* Sidebar \*/}

&#x20; <Sidebar />



&#x20; {/\* Main content \*/}

&#x20; <main className="flex-1 flex flex-col overflow-hidden">

&#x20;   <SearchBar />

&#x20;   <div className="flex-1 overflow-y-auto p-6 grid grid-cols-\[1fr\_320px] gap-6">

&#x20;     <div className="flex flex-col gap-6">

&#x20;       <AIIdePanel />

&#x20;       <AssessmentsTable />

&#x20;       <div className="grid grid-cols-3 gap-6">

&#x20;         <DeadlinesCard />

&#x20;         <NotificationsCard />

&#x20;         <ActivityCard />  {/\* or push to right panel \*/}

&#x20;       </div>

&#x20;     </div>

&#x20;     <aside className="flex flex-col gap-6">

&#x20;       <PerformanceChart />

&#x20;       <LanguageProficiencyChart />

&#x20;       <TeamActivityFeed />

&#x20;     </aside>

&#x20;   </div>

&#x20; </main>

</div>



ANIMATION PATTERNS

1\. IDE panel glow border: CSS @keyframes borderGlow pulsing opacity 1 -> 0.6 -> 1, 3s infinite

2\. Particle background: motion.div with animate={{ y: \[0, -20, 0], opacity: \[0.3, 0.8, 0.3] }}, transition={{ duration: 4â€“8s, repeat: Infinity, delay: staggered }}

3\. Card entrance: motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}, staggered delay per card (0.1s increments)

4\. Chart lines: recharts strokeDasharray animation on mount (animationDuration=1500)

5\. Sidebar nav active indicator: motion.div layoutId="activeNav" for smooth sliding active state

6\. Code cursor blink: CSS animation blink 1s step-end infinite, opacity 0/1



DESIGN PATTERNS USED THROUGHOUT

* Every panel card: liquid-glass rounded-2xl, border border-white/8

* Featured AI IDE panel: liquid-glass-neon rounded-2xl (glowing cyan/purple border)

* Section labels: text-white font-body font-semibold text-base

* Sub-labels / meta: text-white/40 font-body text-xs font-light

* Cyan accent color (#00e5ff): active states, progress values, key metrics, chart line 2

* Purple accent color (#a855f7): status badges, left-border accents on list items, bar chart fill

* Pink/magenta (#ec4899): chart line 1 (performance score)

* All chart containers: liquid-glass rounded-2xl p-4

* Dot/pixel decorative elements: scattered absolute-positioned 4px or 6px squares in cyan/purple/white at low opacity around the IDE panel

* Scrollbar styling: scrollbar-thin scrollbar-track-transparent scrollbar-thumb-white/10






