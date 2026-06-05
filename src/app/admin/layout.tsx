"use client";

import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";
import { getStoredUser, hasStoredAuth } from "@/lib/api";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const router = useRouter();
  const [isAuthorized, setIsAuthorized] = useState(false);

  useEffect(() => {
    const user = getStoredUser();

    if (!hasStoredAuth()) {
      router.replace("/login");
      return;
    }

    if (user?.role !== "administrator") {
      router.replace("/student/dashboard");
      return;
    }

    setIsAuthorized(true);
  }, [router]);

  if (!isAuthorized) {
    return null;
  }

  return (
    <div className="page-shell bg-grid flex h-screen overflow-hidden">
      <ParticleBackground />
      <AppSidebar role="administrator" />
      <div className="relative flex min-w-0 flex-1 flex-col h-screen overflow-hidden">
        <main className="relative flex-1 overflow-y-auto p-4 lg:p-6">
          <TopBar label="Search admin pages..." role="administrator" />
          {children}
        </main>
      </div>
    </div>
  );
}
