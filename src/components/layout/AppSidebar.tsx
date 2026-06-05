"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { logout } from "@/lib/api";
import { SemanticIcon, type SemanticIconName } from "@/components/ui/SemanticIcon";

interface NavItem {
  label: string;
  href: string;
  icon: SemanticIconName;
}

const studentNav: NavItem[] = [
  { label: "Dashboard", href: "/student/dashboard", icon: "dashboard" },
  { label: "Assessments", href: "/student/assessments", icon: "assessments" },
  { label: "Results", href: "/student/results", icon: "results" }
];

const adminNav: NavItem[] = [
  { label: "Dashboard", href: "/admin/dashboard", icon: "dashboard" },
  { label: "Assessments", href: "/admin/assessments", icon: "assessments" },
  { label: "Create", href: "/admin/assessments/new", icon: "create" },
  { label: "Reports", href: "/admin/reports", icon: "reports" },
  { label: "Users", href: "/admin/users", icon: "users" }
];

export function AppSidebar({ role }: { role: "student" | "administrator" }) {
  const pathname = usePathname();
  const router = useRouter();
  const navItems = role === "student" ? studentNav : adminNav;
  const compact = pathname.includes("/workspace");

  return (
    <aside className={`liquid-glass sticky top-0 hidden h-screen shrink-0 flex-col border-r border-white/5 py-5 lg:flex ${compact ? "w-16 px-2" : "w-64 px-4"}`}>
      <Link href={role === "student" ? "/student/dashboard" : "/admin/dashboard"} className={`relative flex items-center ${compact ? "justify-center" : "gap-3"}`}>
        <span className="float-soft grid h-10 w-10 place-items-center rounded-2xl border border-cyanGlow/25 bg-[linear-gradient(145deg,rgba(0,229,255,0.16),rgba(168,85,247,0.12))] text-cyanGlow shadow-[0_0_24px_rgba(0,229,255,0.24)]">
          <SemanticIcon name="platform" size={22} />
        </span>
        <span className={compact ? "sr-only" : ""}>
          <span className="live-gradient-text block text-sm font-semibold">AI Coding</span>
          <span className="block text-xs text-white/40">Assessment Platform</span>
        </span>
      </Link>

      <nav className="relative mt-10 flex flex-col gap-1">
        {navItems.map((item) => {
          let active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          if (pathname.includes("/review")) {
            if (item.href === "/student/results") {
              active = true;
            } else if (item.href === "/student/assessments") {
              active = false;
            }
          }
          return (
            <Link
              key={item.href}
              href={item.href}
              title={compact ? item.label : undefined}
              className={`relative flex items-center gap-3 rounded-xl border px-4 py-2.5 text-sm transition ${
                active ? "border-cyanGlow/30 bg-white/8 text-cyanGlow shadow-[0_0_22px_rgba(0,229,255,0.10)]" : "border-transparent text-white/55 hover:bg-white/5 hover:text-white"
              } ${compact ? "justify-center px-0" : ""}`}
            >
              {active ? <span className="absolute left-0 top-2 h-6 w-0.5 rounded-full bg-cyanGlow shadow-[0_0_12px_rgba(0,229,255,0.8)]" /> : null}
              <SemanticIcon name={item.icon} size={18} />
              <span className={compact ? "sr-only" : ""}>{item.label}</span>
            </Link>
          );
        })}
      </nav>

      <div className="relative mt-auto space-y-3">
        <div className={`scanline rounded-2xl border border-white/10 bg-black/20 p-3 text-xs text-white/45 ${compact ? "hidden" : ""}`}>
          Role: <span className="text-white/75">{role === "student" ? "Student" : "Administrator"}</span>
        </div>
        <button
          className={`flex w-full items-center gap-3 rounded-xl px-4 py-2.5 text-sm text-white/45 transition hover:bg-white/5 hover:text-white ${compact ? "justify-center px-0" : ""}`}
          title={compact ? "Logout" : undefined}
          onClick={async () => {
            await logout();
            router.push("/login");
          }}
        >
          <SemanticIcon name="logout" size={17} />
          <span className={compact ? "sr-only" : ""}>Logout</span>
        </button>
        <div className={`flex items-center gap-3 px-4 py-2.5 text-xs text-white/35 ${compact ? "hidden" : ""}`}>
          <SemanticIcon name="settings" size={16} />
          Backend API mode
        </div>
      </div>
    </aside>
  );
}
