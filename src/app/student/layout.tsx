"use client";

import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";
import { useClientAuthState } from "@/lib/useClientAuthState";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";

export default function StudentLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const pathname = usePathname();
  const router = useRouter();
  const authState = useClientAuthState("student");
  const isWorkspace = pathname.includes("/workspace");

  useEffect(() => {
    if (authState === "unauthenticated") {
      router.replace("/login");
      return;
    }

    if (authState === "wrong-role") {
      router.replace("/admin/dashboard");
    }
  }, [authState, router]);

  if (authState !== "authorized") {
    return null;
  }

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
