"use client";

import { Package, Search, Plus, TrendingUp, AlertTriangle, CheckCircle2, XCircle, Box } from "lucide-react";
import type { RunResult } from "@/lib/types";

interface ProductDashboardPreviewProps {
  runResult: RunResult | null;
  runState: "idle" | "running";
}

interface FeatureState {
  status: "skeleton" | "pass" | "fail";
}

const SAMPLE_PRODUCTS = [
  { id: 1, name: "Wireless Keyboard", price: 49.99, stock: 24, category: "Electronics" },
  { id: 2, name: "USB-C Hub", price: 35.00, stock: 0, category: "Electronics" },
  { id: 3, name: "Monitor Stand", price: 79.99, stock: 12, category: "Furniture" },
  { id: 4, name: "Desk Lamp", price: 22.50, stock: 8, category: "Furniture" },
  { id: 5, name: "Webcam HD", price: 64.99, stock: 3, category: "Electronics" },
];

function deriveFeatures(runResult: RunResult | null): Record<string, FeatureState> {
  const features: Record<string, FeatureState> = {
    product_list: { status: "skeleton" },
    product_cards: { status: "skeleton" },
    stats: { status: "skeleton" },
    search: { status: "skeleton" },
    add_product: { status: "skeleton" },
    table: { status: "skeleton" },
  };

  if (!runResult) return features;

  for (const test of runResult.test_results) {
    const name = test.name.toLowerCase();
    const status = test.passed ? "pass" : "fail";

    if (name.includes("product") && (name.includes("list") || name.includes("array") || name.includes("returns"))) {
      features.product_list = { status };
      features.table = { status };
    }
    if (name.includes("card") || name.includes("render") || name.includes("display")) {
      features.product_cards = { status };
    }
    if (name.includes("stat") || name.includes("total") || name.includes("count")) {
      features.stats = { status };
    }
    if (name.includes("search") || name.includes("filter")) {
      features.search = { status };
    }
    if (name.includes("create") || name.includes("add") || name.includes("post")) {
      features.add_product = { status };
    }
    if (name.includes("not found") || name.includes("404") || name.includes("missing")) {
      features.search = { status };
    }
    if (name.includes("discount") || name.includes("price") || name.includes("calculat")) {
      features.stats = { status };
      features.product_cards = { status };
    }
  }

  return features;
}

function StatusDot({ status }: { status: "skeleton" | "pass" | "fail" }) {
  if (status === "pass") return <CheckCircle2 size={14} className="text-emerald-400" />;
  if (status === "fail") return <XCircle size={14} className="text-rose-400" />;
  return <div className="h-3.5 w-3.5 rounded-full bg-white/10" />;
}

function SkeletonBlock({ className = "" }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-white/8 ${className}`} />;
}

export function ProductDashboardPreview({ runResult, runState }: ProductDashboardPreviewProps) {
  const features = deriveFeatures(runResult);
  const hasRun = runResult !== null;
  const allPassed = hasRun && runResult.test_results.every(t => t.passed);
  const totalProducts = SAMPLE_PRODUCTS.length;
  const inStock = SAMPLE_PRODUCTS.filter(p => p.stock > 0).length;

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-white/10 bg-[#0a0f1a]">
      {/* App Header */}
      <div className="flex items-center gap-3 border-b border-white/10 bg-white/[0.03] px-4 py-2.5">
        <div className="flex items-center gap-2">
          <Package size={16} className="text-cyanGlow" />
          <span className="text-sm font-semibold text-white">Product Dashboard</span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          {features.search.status === "skeleton" ? (
            <SkeletonBlock className="h-7 w-32 rounded-lg" />
          ) : (
            <div className={`flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs ${
              features.search.status === "pass"
                ? "border-white/15 bg-white/5 text-white/50"
                : "border-rose-500/30 bg-rose-500/5 text-rose-300/50"
            }`}>
              <Search size={12} />
              <span>Search products...</span>
              <StatusDot status={features.search.status} />
            </div>
          )}
        </div>
      </div>

      <div className="scrollbar-soft flex-1 overflow-y-auto p-4">
        {/* Running Overlay */}
        {runState === "running" && (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-cyanGlow/20 bg-cyanGlow/5 px-3 py-2 text-xs text-cyanGlow">
            <div className="h-2 w-2 animate-pulse rounded-full bg-cyanGlow" />
            Building and testing your code...
          </div>
        )}

        {/* Stats Row */}
        <div className="mb-4 grid grid-cols-3 gap-3">
          <StatCard
            label="Total Products"
            value={hasRun ? String(totalProducts) : "--"}
            icon={<Box size={14} />}
            status={features.stats.status}
          />
          <StatCard
            label="In Stock"
            value={hasRun ? String(inStock) : "--"}
            icon={<TrendingUp size={14} />}
            status={features.stats.status}
            accent="emerald"
          />
          <StatCard
            label="Out of Stock"
            value={hasRun ? String(totalProducts - inStock) : "--"}
            icon={<AlertTriangle size={14} />}
            status={features.stats.status}
            accent="amber"
          />
        </div>

        {/* Product Cards */}
        <div className="mb-4">
          <div className="mb-2 flex items-center justify-between">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-white/40">Products</h3>
            {features.add_product.status === "skeleton" ? (
              <SkeletonBlock className="h-6 w-24 rounded-lg" />
            ) : (
              <div className={`flex items-center gap-1 rounded-lg border px-2 py-1 text-[10px] ${
                features.add_product.status === "pass"
                  ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-300"
                  : "border-rose-500/30 bg-rose-500/10 text-rose-300"
              }`}>
                <Plus size={10} />
                Add Product
                <StatusDot status={features.add_product.status} />
              </div>
            )}
          </div>
          <div className="grid grid-cols-3 gap-2">
            {features.product_cards.status === "skeleton" ? (
              <>
                <SkeletonBlock className="h-20 rounded-xl" />
                <SkeletonBlock className="h-20 rounded-xl" />
                <SkeletonBlock className="h-20 rounded-xl" />
              </>
            ) : (
              SAMPLE_PRODUCTS.slice(0, 3).map((product) => (
                <div
                  key={product.id}
                  className={`rounded-xl border p-2.5 ${
                    features.product_cards.status === "pass"
                      ? "border-white/10 bg-white/[0.03]"
                      : "border-rose-500/20 bg-rose-500/[0.03]"
                  }`}
                >
                  <p className="text-xs font-medium text-white/80">{product.name}</p>
                  <p className="mt-0.5 text-[11px] text-cyanGlow">${product.price.toFixed(2)}</p>
                  <div className="mt-1 flex items-center justify-between">
                    <span className={`text-[10px] ${product.stock > 0 ? "text-emerald-400/70" : "text-rose-400/70"}`}>
                      {product.stock > 0 ? `${product.stock} in stock` : "Out of stock"}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Product Table */}
        <div>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-white/40">Inventory Table</h3>
          {features.table.status === "skeleton" ? (
            <div className="space-y-1.5">
              <SkeletonBlock className="h-7 w-full rounded-lg" />
              <SkeletonBlock className="h-6 w-full rounded" />
              <SkeletonBlock className="h-6 w-full rounded" />
              <SkeletonBlock className="h-6 w-full rounded" />
            </div>
          ) : (
            <div className={`overflow-hidden rounded-lg border ${
              features.table.status === "pass" ? "border-white/10" : "border-rose-500/20"
            }`}>
              <table className="w-full text-[11px]">
                <thead>
                  <tr className="border-b border-white/10 bg-white/[0.03]">
                    <th className="px-2.5 py-1.5 text-left font-semibold text-white/50">ID</th>
                    <th className="px-2.5 py-1.5 text-left font-semibold text-white/50">Name</th>
                    <th className="px-2.5 py-1.5 text-left font-semibold text-white/50">Price</th>
                    <th className="px-2.5 py-1.5 text-left font-semibold text-white/50">Stock</th>
                    <th className="px-2.5 py-1.5 text-left font-semibold text-white/50">Category</th>
                  </tr>
                </thead>
                <tbody>
                  {SAMPLE_PRODUCTS.map((product) => (
                    <tr key={product.id} className="border-b border-white/5 last:border-0">
                      <td className="px-2.5 py-1.5 text-white/40">{product.id}</td>
                      <td className="px-2.5 py-1.5 text-white/70">{product.name}</td>
                      <td className="px-2.5 py-1.5 text-cyanGlow/70">${product.price.toFixed(2)}</td>
                      <td className="px-2.5 py-1.5">
                        <span className={product.stock > 0 ? "text-emerald-400/70" : "text-rose-400/70"}>
                          {product.stock}
                        </span>
                      </td>
                      <td className="px-2.5 py-1.5 text-white/40">{product.category}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Overall Status Footer */}
        {hasRun && (
          <div className={`mt-4 rounded-lg border px-3 py-2 text-xs ${
            allPassed
              ? "border-emerald-500/30 bg-emerald-500/5 text-emerald-300"
              : "border-amber-500/30 bg-amber-500/5 text-amber-300"
          }`}>
            {allPassed
              ? "All features working correctly. The dashboard is fully functional."
              : `${runResult.test_results.filter(t => t.passed).length}/${runResult.test_results.length} tests passing. Some features need attention.`
            }
          </div>
        )}
      </div>
    </div>
  );
}

function StatCard({ label, value, icon, status, accent }: {
  label: string;
  value: string;
  icon: React.ReactNode;
  status: "skeleton" | "pass" | "fail";
  accent?: "emerald" | "amber";
}) {
  if (status === "skeleton") {
    return (
      <div className="rounded-xl border border-white/10 bg-white/[0.02] p-2.5">
        <SkeletonBlock className="mb-1.5 h-3 w-16" />
        <SkeletonBlock className="h-5 w-10" />
      </div>
    );
  }

  const accentColor = accent === "emerald"
    ? "text-emerald-400"
    : accent === "amber"
      ? "text-amber-400"
      : "text-cyanGlow";

  return (
    <div className={`rounded-xl border p-2.5 ${
      status === "pass" ? "border-white/10 bg-white/[0.02]" : "border-rose-500/20 bg-rose-500/[0.02]"
    }`}>
      <div className="flex items-center gap-1.5 text-white/40">
        {icon}
        <span className="text-[10px]">{label}</span>
        <span className="ml-auto"><StatusDot status={status} /></span>
      </div>
      <p className={`mt-1 text-lg font-bold ${accentColor}`}>{value}</p>
    </div>
  );
}
