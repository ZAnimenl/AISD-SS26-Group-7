import { UserManagementClient } from "@/components/admin/UserManagementClient";
import { SectionHeader } from "@/components/ui/SectionHeader";

export default function AdminUsersPage() {
  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Users" />
      <UserManagementClient />
    </div>
  );
}
