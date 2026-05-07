"use client";

import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";
import { usePathname } from "next/navigation";

export default function StudentLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const pathname = usePathname();
  const isWorkspace = pathname.includes("/workspace");

  return (
    <div className="page-shell bg-grid flex overflow-x-hidden">
      <ParticleBackground />
      <AppSidebar role="student" />
      <div className="relative flex min-w-0 flex-1 flex-col">
        {isWorkspace ? null : <TopBar label="Search student pages..." role="student" />}
        <main className={`relative min-w-0 flex-1 ${isWorkspace ? "overflow-hidden p-2 lg:p-3" : "p-3 lg:p-4"}`}>{children}</main>
      </div>
    </div>
  );
}
