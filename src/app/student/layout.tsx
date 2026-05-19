"use client";

import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";
import { usePathname } from "next/navigation";

export default function StudentLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const pathname = usePathname();
  const isWorkspace = pathname.includes("/workspace");

  return (
    <div className="page-shell bg-grid flex h-screen overflow-hidden">
      <ParticleBackground />
      <AppSidebar role="student" />
      <div className="relative flex min-w-0 flex-1 flex-col h-screen overflow-hidden">
        <main className={`relative min-w-0 flex-1 overflow-y-auto ${isWorkspace ? "overflow-hidden p-2 lg:p-3" : "p-3 lg:p-4"}`}>
          {isWorkspace ? null : <TopBar label="Search student pages..." role="student" />}
          {children}
        </main>
      </div>
    </div>
  );
}
