import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";

export default function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <div className="page-shell bg-grid flex">
      <ParticleBackground />
      <AppSidebar role="administrator" />
      <div className="relative flex min-w-0 flex-1 flex-col">
        <TopBar label="Administrator assessment authoring and reporting" />
        <main className="relative flex-1 p-4 lg:p-6">{children}</main>
      </div>
    </div>
  );
}
