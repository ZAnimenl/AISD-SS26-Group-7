import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AI Coding Assessment",
  description: "AI-assisted coding assessment platform"
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" data-scroll-behavior="smooth">
      <body suppressHydrationWarning>{children}</body>
    </html>
  );
}
