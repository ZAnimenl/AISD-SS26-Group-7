import { AppSidebar } from "@/components/layout/AppSidebar";
import { ParticleBackground } from "@/components/layout/ParticleBackground";
import { TopBar } from "@/components/layout/TopBar";

export default function StudentLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <div className="page-shell bg-grid flex">
      <ParticleBackground />
      <AppSidebar role="student" />
      <div className="relative flex min-w-0 flex-1 flex-col">
        <TopBar label="Student workspace, assessments, and results" />
        <main className="relative flex-1 p-4 lg:p-6">{children}</main>
      </div>
    </div>
  );
}
