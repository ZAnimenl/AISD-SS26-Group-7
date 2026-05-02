"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { BarChart3, Code2, FileText, LayoutDashboard, LogOut, PlusCircle, Settings, Sparkles, type LucideIcon } from "lucide-react";
import { logout } from "@/lib/api";

interface NavItem {
  label: string;
  href: string;
  icon: LucideIcon;
}

const studentNav: NavItem[] = [
  { label: "Dashboard", href: "/student/dashboard", icon: LayoutDashboard },
  { label: "Assessments", href: "/student/assessments", icon: Code2 },
  { label: "Results", href: "/student/results", icon: BarChart3 }
];

const adminNav: NavItem[] = [
  { label: "Dashboard", href: "/admin/dashboard", icon: LayoutDashboard },
  { label: "Assessments", href: "/admin/assessments", icon: FileText },
  { label: "Create", href: "/admin/assessments/new", icon: PlusCircle },
  { label: "Reports", href: "/admin/reports", icon: BarChart3 }
];

export function AppSidebar({ role }: { role: "student" | "administrator" }) {
  const pathname = usePathname();
  const router = useRouter();
  const navItems = role === "student" ? studentNav : adminNav;

  return (
    <aside className="liquid-glass sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-white/5 px-4 py-5 lg:flex">
      <Link href={role === "student" ? "/student/dashboard" : "/admin/dashboard"} className="relative flex items-center gap-3">
        <span className="grid h-10 w-10 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow shadow-[0_0_20px_rgba(0,229,255,0.22)]">
          <Sparkles size={20} />
        </span>
        <span>
          <span className="block text-sm font-semibold text-white">AI Coding</span>
          <span className="block text-xs text-white/40">Assessment MVP</span>
        </span>
      </Link>

      <nav className="relative mt-10 flex flex-col gap-1">
        {navItems.map((item) => {
          const Icon = item.icon;
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={`relative flex items-center gap-3 rounded-xl border px-4 py-2.5 text-sm transition ${
                active ? "border-cyanGlow/20 bg-white/8 text-cyanGlow" : "border-transparent text-white/55 hover:bg-white/5 hover:text-white"
              }`}
            >
              {active ? <span className="absolute left-0 top-2 h-6 w-0.5 rounded-full bg-cyanGlow" /> : null}
              <Icon size={18} />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="relative mt-auto space-y-3">
        <div className="rounded-2xl border border-white/10 bg-black/20 p-3 text-xs text-white/45">
          Role: <span className="text-white/75">{role === "student" ? "Student" : "Administrator"}</span>
        </div>
        <button
          className="flex w-full items-center gap-3 rounded-xl px-4 py-2.5 text-sm text-white/45 transition hover:bg-white/5 hover:text-white"
          onClick={async () => {
            await logout();
            router.push("/login");
          }}
        >
          <LogOut size={17} />
          Logout
        </button>
        <div className="flex items-center gap-3 px-4 py-2.5 text-xs text-white/35">
          <Settings size={16} />
          Backend API mode
        </div>
      </div>
    </aside>
  );
}
