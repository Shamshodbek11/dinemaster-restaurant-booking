import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Menu, X, UtensilsCrossed, Globe, Sun, Moon } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";
import { useApp } from "@/contexts/AppContext";
import { NotificationBell } from "@/components/NotificationBell";
import { t, languages } from "@/lib/i18n";
import { brandParts } from "@/lib/brand";
import { canManage } from "@/lib/api";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

export const Navbar = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();
  const { language, setLanguage, user, darkMode, setDarkMode } = useApp();
  const roles = (user?.roles ?? []).map(role => role.toLowerCase());
  const accountPath = roles.includes('superadmin') ? '/superadmin-dashboard' : canManage(user) ? '/manager' : '/profile';
  const [brandA, brandB] = brandParts();
  const themeLabel = darkMode ? t('theme.light', language) : t('theme.dark', language);

  const navLinks = [
    { label: t('nav.restaurants', language), href: "/restaurants" },
    { label: t('nav.venues', language), href: "/venues" },
    { label: t('nav.offers', language), href: "/offers" },
    { label: t('nav.yums', language), href: "/yums" },
  ];

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 glass">
      <div className="container mx-auto flex items-center justify-between h-16 px-4">
        <Link to="/" className="flex items-center gap-2 group">
          <div className="w-9 h-9 rounded-lg bg-gradient-amber flex items-center justify-center shadow-glow group-hover:scale-105 transition-transform">
            <UtensilsCrossed className="w-5 h-5 text-primary-foreground" />
          </div>
          <span className="font-display text-xl font-bold text-foreground">
            {brandA}<span className="text-gradient-amber">{brandB}</span>
          </span>
        </Link>

        <div className="hidden md:flex items-center gap-8">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              to={link.href}
              className={`text-sm font-medium transition-colors duration-200 ${
                location.pathname === link.href ? "text-primary" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              {link.label}
            </Link>
          ))}
        </div>

        <div className="hidden md:flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={() => setDarkMode(!darkMode)} aria-label={themeLabel} title={themeLabel}>
            {darkMode ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" aria-label={t('common.language', language)}><Globe className="w-4 h-4" /></Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="bg-card border-border">
              {languages.map(l => (
                <DropdownMenuItem key={l.code} onClick={() => setLanguage(l.code)} className={language === l.code ? 'text-primary' : ''}>
                  {l.flag} {l.label}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>

          <NotificationBell />
          {user ? (
            <Button variant="navCta" size="sm" onClick={() => navigate(accountPath)}>
              {t('nav.profile', language)}
            </Button>
          ) : (
            <>
              <Button variant="ghost" size="sm" asChild>
                <Link to="/login">{t('nav.login', language)}</Link>
              </Button>
              <Button variant="navCta" size="sm" asChild>
                <Link to="/signup">{t('nav.signup', language)}</Link>
              </Button>
            </>
          )}
        </div>

        <div className="md:hidden flex items-center gap-1">
          <NotificationBell />
          <button className="text-foreground p-1" onClick={() => setMobileOpen(!mobileOpen)} aria-label={t('common.menu', language)}>
            {mobileOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
          </button>
        </div>
      </div>

      <AnimatePresence>
        {mobileOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            exit={{ opacity: 0, height: 0 }}
            className="md:hidden glass border-t border-border"
          >
            <div className="container mx-auto px-4 py-4 flex flex-col gap-3">
              {navLinks.map((link) => (
                <Link key={link.href} to={link.href} onClick={() => setMobileOpen(false)} className="text-sm font-medium text-muted-foreground hover:text-foreground py-2">
                  {link.label}
                </Link>
              ))}
              <div className="flex gap-2 py-2">
                {languages.map(l => (
                  <button key={l.code} onClick={() => setLanguage(l.code)} className={`text-sm px-3 py-1 rounded-lg ${language === l.code ? 'bg-primary text-primary-foreground' : 'bg-secondary text-foreground'}`}>
                    {l.flag}
                  </button>
                ))}
                <button onClick={() => setDarkMode(!darkMode)} aria-label={themeLabel}
                  className="ml-auto text-sm px-3 py-1 rounded-lg bg-secondary text-foreground flex items-center gap-1.5">
                  {darkMode ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />} {themeLabel}
                </button>
              </div>
              <div className="flex gap-3 pt-2">
                {user ? (
                  <Button variant="navCta" size="sm" className="flex-1" onClick={() => { setMobileOpen(false); navigate(accountPath); }}>
                    {t('nav.profile', language)}
                  </Button>
                ) : (
                  <>
                    <Button variant="outline" size="sm" className="flex-1" asChild>
                      <Link to="/login" onClick={() => setMobileOpen(false)}>{t('nav.login', language)}</Link>
                    </Button>
                    <Button variant="navCta" size="sm" className="flex-1" asChild>
                      <Link to="/signup" onClick={() => setMobileOpen(false)}>{t('nav.signup', language)}</Link>
                    </Button>
                  </>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </nav>
  );
};
