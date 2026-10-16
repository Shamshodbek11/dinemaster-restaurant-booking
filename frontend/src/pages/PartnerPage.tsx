import { Link } from 'react-router-dom';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { Button } from '@/components/ui/button';
import { PartnerApplicationForm } from '@/components/PartnerApplicationForm';
import { useApp } from '@/contexts/AppContext';
import { t } from '@/lib/i18n';
import { CalendarCheck, BarChart3, Users, Store } from 'lucide-react';

/** "Restoran egasimisiz? → Hoziroq qo'shiling" sahifasi. */
const PartnerPage = () => {
  const { user, language } = useApp();

  const benefits = [
    { icon: CalendarCheck, title: t('partner.benefit1', language) },
    { icon: Users, title: t('partner.benefit2', language) },
    { icon: BarChart3, title: t('partner.benefit3', language) },
    { icon: Store, title: t('partner.benefit4', language) },
  ];

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="pt-24 pb-16 px-4">
        <div className="container mx-auto max-w-5xl grid lg:grid-cols-2 gap-10">
          <section>
            <h1 className="font-display text-4xl font-bold text-foreground mb-4">{t('cta.title', language)}</h1>
            <p className="text-muted-foreground text-lg mb-8">{t('cta.subtitle', language)}</p>
            <ul className="space-y-4">
              {benefits.map(b => (
                <li key={b.title} className="flex items-center gap-3 text-foreground">
                  <span className="w-10 h-10 rounded-xl bg-primary/15 flex items-center justify-center shrink-0">
                    <b.icon className="w-5 h-5 text-primary" />
                  </span>
                  {b.title}
                </li>
              ))}
            </ul>
            <p className="text-sm text-muted-foreground mt-8">{t('partner.howItWorks', language)}</p>
          </section>

          <section>
            {user ? (
              <PartnerApplicationForm />
            ) : (
              <div className="bg-card rounded-xl border border-border p-6 space-y-4">
                <p className="text-foreground">{t('partner.loginFirst', language)}</p>
                <div className="flex gap-3">
                  <Link to="/signup?next=/partner"><Button variant="hero">{t('nav.signup', language)}</Button></Link>
                  <Link to="/login?next=/partner"><Button variant="outline">{t('nav.login', language)}</Button></Link>
                </div>
              </div>
            )}
          </section>
        </div>
      </main>
      <Footer />
    </div>
  );
};

export default PartnerPage;
