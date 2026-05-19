import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";

export default function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
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
