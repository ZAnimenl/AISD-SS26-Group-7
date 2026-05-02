import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        heading: ["'Instrument Serif'", "serif"],
        body: ["Barlow", "sans-serif"],
        mono: ["'JetBrains Mono'", "monospace"]
      },
      colors: {
        cyanGlow: "#00e5ff",
        purpleGlow: "#a855f7",
        pinkGlow: "#ec4899"
      }
    }
  },
  plugins: []
};

export default config;
