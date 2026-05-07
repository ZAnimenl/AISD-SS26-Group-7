const particles = [
  ["left-[8%] top-[14%]", "bg-cyanGlow/50", "0s"],
  ["left-[28%] top-[78%]", "bg-purpleGlow/50", "0.6s"],
  ["left-[48%] top-[18%]", "bg-white/30", "1.2s"],
  ["left-[72%] top-[64%]", "bg-cyanGlow/40", "1.8s"],
  ["left-[88%] top-[24%]", "bg-pinkGlow/40", "2.4s"],
  ["left-[16%] top-[52%]", "bg-purpleGlow/40", "3s"],
  ["left-[38%] top-[44%]", "bg-cyanGlow/35", "3.6s"],
  ["left-[62%] top-[86%]", "bg-white/25", "4.2s"],
  ["left-[82%] top-[80%]", "bg-purpleGlow/35", "4.8s"]
];

export function ParticleBackground() {
  return (
    <div aria-hidden="true" className="pointer-events-none fixed inset-0 overflow-hidden">
      <div className="absolute left-[12%] top-[18%] h-32 w-32 rounded-full border border-cyanGlow/10 opacity-50 blur-[1px]" style={{ animation: "floatSoft 9s ease-in-out infinite" }} />
      <div className="absolute right-[10%] top-[28%] h-44 w-44 rounded-full border border-purpleGlow/10 opacity-50 blur-[1px]" style={{ animation: "floatSoft 11s ease-in-out infinite reverse" }} />
      {particles.map(([position, color, delay], index) => (
        <span
          key={`${position}-${color}`}
          className={`absolute h-1.5 w-1.5 rounded-sm ${position} ${color}`}
          style={{ animation: `floatSoft ${4 + index * 0.45}s ease-in-out ${delay} infinite` }}
        />
      ))}
    </div>
  );
}
