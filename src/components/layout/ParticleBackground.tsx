const particles = [
  ["left-[8%] top-[14%]", "bg-cyanGlow/50"],
  ["left-[28%] top-[78%]", "bg-purpleGlow/50"],
  ["left-[48%] top-[18%]", "bg-white/30"],
  ["left-[72%] top-[64%]", "bg-cyanGlow/40"],
  ["left-[88%] top-[24%]", "bg-pinkGlow/40"],
  ["left-[16%] top-[52%]", "bg-purpleGlow/40"]
];

export function ParticleBackground() {
  return (
    <div aria-hidden="true" className="pointer-events-none fixed inset-0 overflow-hidden">
      {particles.map(([position, color], index) => (
        <span
          key={`${position}-${color}`}
          className={`absolute h-1.5 w-1.5 rounded-sm ${position} ${color}`}
          style={{ animation: `borderGlow ${4 + index * 0.5}s ease-in-out infinite` }}
        />
      ))}
    </div>
  );
}
