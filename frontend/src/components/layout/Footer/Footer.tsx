'use client';

import Link from 'next/link';
import { useState } from 'react';
import { LegalModal } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import styles from './Footer.module.scss';

export function Footer() {
  const [showLegalModal, setShowLegalModal] = useState(false);
  const { isAuthenticated } = useAuth();

  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <>
      <footer className={styles.footer}>
        <div className={styles.container}>
          <div className={styles.footerContent}>
            <div className={styles.footerSection}>
              <h4>Navigation</h4>
              <ul>
                <li><Link href="/"><i className="fas fa-home"></i> Home</Link></li>
                <li><Link href="/buses"><i className="fas fa-bus"></i> Bus Information</Link></li>
                {isAuthenticated ? (
                  <li><Link href="/settings"><i className="fas fa-cog"></i> Account Settings</Link></li>
                ) : (
                  <li><Link href="/login"><i className="fas fa-sign-in-alt"></i> Sign In</Link></li>
                )}
              </ul>
            </div>
            <div className={styles.footerSection}>
              <h4>Legal Information</h4>
              <ul>
                <li><Link href="/privacy"><i className="fas fa-shield-alt"></i> Privacy Policy</Link></li>
                <li><Link href="/terms"><i className="fas fa-gavel"></i> Terms of Use</Link></li>
                <li>
                  <button onClick={() => setShowLegalModal(true)} className={styles.linkButton}>
                    <i className="fas fa-balance-scale"></i> Legal Notice
                  </button>
                </li>
                <li><Link href="/disclaimer"><i className="fas fa-exclamation-circle"></i> Independence Disclaimer</Link></li>
              </ul>
            </div>
            <div className={styles.footerSection}>
              <h4>About</h4>
              <ul>
                <li>
                  <Link href="/about">
                    <i className="fas fa-info-circle"></i> About This Project
                  </Link>
                </li>
                <li>
                  <a href="https://github.com/Jacob-Walton/buses-info" target="_blank" rel="noopener noreferrer">
                    <i className="fab fa-github"></i> View Source Code
                  </a>
                </li>
                <li>
                  <a href="mailto:jacob-walton@konpeki.co.uk">
                    <i className="fas fa-envelope"></i> Contact Developer
                  </a>
                </li>
              </ul>
            </div>
          </div>
          <div className={styles.footerBottom}>
            <div className={styles.footerLinks}>
              <button onClick={scrollToTop} className={styles.scrollTopButton}>
                <i className="fas fa-arrow-up"></i> Back to Top
              </button>
            </div>
            <div className={styles.copyright}>
              &copy; {new Date().getFullYear()} - Bus Info Project - An Independent Open Source Project
              <div className={styles.disclaimerText}>
                Not affiliated with or endorsed by any educational institution
              </div>
            </div>
          </div>
        </div>
      </footer>

      <LegalModal 
        isOpen={showLegalModal}
        onAccept={() => setShowLegalModal(false)}
        onClose={() => setShowLegalModal(false)}
      />
    </>
  );
}