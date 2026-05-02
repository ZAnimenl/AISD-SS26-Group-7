"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { UserPlus } from "lucide-react";
import { createAdminUser, getAdminUsers } from "@/lib/api";
import type { Role, UserAccount } from "@/lib/types";

export function UserManagementClient() {
  const router = useRouter();
  const [users, setUsers] = useState<UserAccount[]>([]);
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<Role>("administrator");
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    getAdminUsers().then(setUsers).catch(() => router.replace("/login"));
  }, [router]);

  async function handleCreateUser(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setStatus(null);
    setError(null);
    setIsSubmitting(true);

    try {
      const user = await createAdminUser({
        full_name: fullName,
        email,
        password,
        role,
        status: "active"
      });
      setUsers((current) => [...current, user].sort((left, right) => left.full_name.localeCompare(right.full_name)));
      setFullName("");
      setEmail("");
      setPassword("");
      setRole("administrator");
      setStatus("User account created.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Unable to create user.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
      <section className="panel">
        <form className="relative grid gap-4" onSubmit={handleCreateUser}>
          <h2 className="text-lg font-semibold">Create account</h2>
          <label className="grid gap-2 text-sm text-white/60">
            Full name
            <input className="field" value={fullName} onChange={(event) => setFullName(event.target.value)} required />
          </label>
          <label className="grid gap-2 text-sm text-white/60">
            Email
            <input className="field" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
          </label>
          <label className="grid gap-2 text-sm text-white/60">
            Password
            <input className="field" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={6} />
          </label>
          <label className="grid gap-2 text-sm text-white/60">
            Role
            <select className="field" value={role} onChange={(event) => setRole(event.target.value as Role)}>
              <option value="administrator">administrator</option>
              <option value="student">student</option>
            </select>
          </label>
          <button className="btn-primary w-fit" type="submit" disabled={isSubmitting}>
            <UserPlus size={16} />
            {isSubmitting ? "Creating..." : "Create user"}
          </button>
          {status ? <p className="text-sm text-cyanGlow">{status}</p> : null}
          {error ? <p className="text-sm text-pinkGlow">{error}</p> : null}
        </form>
      </section>

      <section className="panel">
        <div className="relative overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="text-xs uppercase tracking-[0.14em] text-white/35">
              <tr>
                <th className="pb-3">Name</th>
                <th className="pb-3">Email</th>
                <th className="pb-3">Role</th>
                <th className="pb-3">Status</th>
                <th className="pb-3">Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/10">
              {users.map((user) => (
                <tr key={user.user_id}>
                  <td className="py-4 font-semibold text-white">{user.full_name}</td>
                  <td className="py-4 text-white/60">{user.email}</td>
                  <td className="py-4 text-white/60">{user.role}</td>
                  <td className="py-4 text-white/60">{user.status}</td>
                  <td className="py-4 text-white/45">{user.created_at ? new Date(user.created_at).toLocaleString() : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
