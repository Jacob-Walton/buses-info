import { Metadata } from 'next';
import { Navbar, Footer } from '@/components/layout';
import { CookieConsent } from '@/components/common';
import { AuthProvider } from '@/providers/AuthProvider';
import './globals.scss';
import { GoogleOAuthProvider } from '@react-oauth/google';

export const metadata: Metadata = {
  title: 'Bus Info',
  description: 'Unofficial bus arrival information',
  robots: 'noindex, nofollow',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <GoogleOAuthProvider clientId={process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID || ''}>
          <AuthProvider>
            <div className="content-wrapper">
              <Navbar />
              <main role="main">{children}</main>
              <Footer />
            </div>
            <CookieConsent />
          </AuthProvider>
        </GoogleOAuthProvider>
      </body>
    </html>
  );
}
