import { defineConfig, loadEnv, type Plugin } from "vite";
import react from "@vitejs/plugin-react-swc";
import path from "path";

// index.html dagi %BRAND% → dastur nomi (VITE_BRAND_NAME, standart: DineMaster)
const brandHtml = (brand: string): Plugin => ({
  name: "brand-html",
  transformIndexHtml: (html) => html.split("%BRAND%").join(brand),
});

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => ({
  server: {
    host: "::",
    port: 8080,
    // 8080 band bo'lsa boshqa portga (8081, 8082...) o'tib ketmaydi — xato beradi.
    // Aks holda backend CORS ruxsat bermagan portda sayt ochilib, API so'rovlari ishlamay qoladi.
    // Xato chiqsa: frontend allaqachon boshqa oynada ishlayapti — o'shani ishlating yoki yoping.
    strictPort: true,
    // API so'rovlari backendga shu server orqali uzatiladi: brauzer (shu jumladan telefon)
    // faqat 8080 ga murojaat qiladi — backendni tarmoqqa ochish va CORS sozlash shart emas.
    proxy: {
      "/api": { target: "http://localhost:5000", changeOrigin: true },
      "/health": { target: "http://localhost:5000", changeOrigin: true },
    },
    hmr: {
      overlay: false,
    },
  },
  plugins: [react(), brandHtml(loadEnv(mode, process.cwd(), "VITE_").VITE_BRAND_NAME?.trim() || "DineMaster")],
  build: {
    rollupOptions: {
      output: {
        // Katta kutubxonalar alohida fayllarda — brauzer keshida uzoq saqlanadi
        manualChunks: {
          react: ["react", "react-dom", "react-router-dom"],
          motion: ["framer-motion"],
          charts: ["recharts"],
          dates: ["date-fns"],
        },
      },
    },
  },
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
}));
