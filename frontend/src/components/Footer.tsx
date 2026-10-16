import { UtensilsCrossed } from "lucide-react";
import { Link } from "react-router-dom";
import { useApp } from "@/contexts/AppContext";
import { t } from "@/lib/i18n";
import { BRAND, brandParts } from "@/lib/brand";

export const Footer = () => {
  const { language } = useApp();
  const [brandA, brandB] = brandParts();
  const link = "text-sm text-muted-foreground hover:text-foreground transition-colors";

  return (
    <footer className="border-t border-border bg-card/50">
      <div className="container mx-auto px-4 py-12">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-8">
          <div className="col-span-2 md:col-span-1">
            <Link to="/" className="flex items-center gap-2 mb-4">
              <div className="w-8 h-8 rounded-lg bg-gradient-amber flex items-center justify-center">
                <UtensilsCrossed className="w-4 h-4 text-primary-foreground" />
              </div>
              <span className="font-display text-lg font-bold text-foreground">
                {brandA}<span className="text-gradient-amber">{brandB}</span>
              </span>
            </Link>
            <p className="text-muted-foreground text-sm">{t('hero.subtitle', language)}</p>
          </div>
          <div>
            <h4 className="font-display text-sm font-semibold text-foreground mb-4">{t('footer.guests', language)}</h4>
            <div className="flex flex-col gap-2">
              <Link to="/restaurants" className={link}>{t('nav.restaurants', language)}</Link>
              <Link to="/venues" className={link}>{t('nav.venues', language)}</Link>
              <Link to="/offers" className={link}>{t('nav.offers', language)}</Link>
              <Link to="/yums" className={link}>{t('nav.yums', language)}</Link>
            </div>
          </div>
          <div>
            <h4 className="font-display text-sm font-semibold text-foreground mb-4">{t('footer.help', language)}</h4>
            <div className="flex flex-col gap-2">
              <Link to="/faq" className={link}>{t('footer.faq', language)}</Link>
              <Link to="/terms" className={link}>{t('footer.terms', language)}</Link>
              <Link to="/privacy" className={link}>{t('footer.privacy', language)}</Link>
            </div>
          </div>
          <div>
            <h4 className="font-display text-sm font-semibold text-foreground mb-4">{t('footer.business', language)}</h4>
            <div className="flex flex-col gap-2">
              <Link to="/partner" className={link}>{t('footer.partner', language)}</Link>
              <Link to="/manager" className={link}>{t('footer.managerLogin', language)}</Link>
            </div>
          </div>
        </div>
        <div className="border-t border-border mt-8 pt-8 text-center">
          <p className="text-xs text-muted-foreground">© {new Date().getFullYear()} {BRAND}. {t('footer.rights', language)}</p>
        </div>
      </div>
    </footer>
  );
};
