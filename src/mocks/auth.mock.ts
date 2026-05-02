import type { MockUser } from "@/lib/types";

export const mockUsers: MockUser[] = [
  {
    user_id: "mock-student-1",
    name: "Mira Student",
    email: "mira.student@example.edu",
    role: "student"
  },
  {
    user_id: "mock-admin-1",
    name: "Ada Admin",
    email: "ada.admin@example.edu",
    role: "administrator"
  }
];
