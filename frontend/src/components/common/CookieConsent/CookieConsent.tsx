'use client';

import { useState, useEffect } from 'react';
import styles from './CookieConsent.module.scss';

interface CookiePreferences {
  essential: boolean;
  analytics: boolean;
  marketing: boolean;
}

const COOKIE_CONSENT_KEY = 'bus-info-cookie-consent';
const COOKIE_PREFERENCES_KEY = 'bus-info-cookie-preferences';

export default function CookieConsent() {
  const [showBanner, setShowBanner] = useState(false);
  const [showDetails, setShowDetails] = useState(false);
  const [preferences, setPreferences] = useState<CookiePreferences>({
    essential: true, // Always required
    analytics: false,
    marketing: false,
  });

  useEffect(() => {
    // Check if user has already provided consent
    const hasConsent = localStorage.getItem(COOKIE_CONSENT_KEY);
    if (!hasConsent) {
      // Delay showing banner slightly
      const timer = setTimeout(() => setShowBanner(true), 1000);
      return () => clearTimeout(timer);
    } else {
      // Load existing preferences
      const savedPreferences = localStorage.getItem(COOKIE_PREFERENCES_KEY);
      if (savedPreferences) {
        try {
          setPreferences(JSON.parse(savedPreferences));
        } catch (e) {
          console.error('Failed to parse cookie preferences:', e);
        }
      }
    }
  }, []);

  const handleAcceptAll = () => {
    const allAccepted: CookiePreferences = {
      essential: true,
      analytics: true,
      marketing: true,
    };

    savePreferences(allAccepted);
    setShowBanner(false);
  };

  const handleAcceptSelected = () => {
    savePreferences(preferences);
    setShowBanner(false);
  };

  const handleRejectAll = () => {
    const minimal: CookiePreferences = {
      essential: true,
      analytics: false,
      marketing: false,
    };

    savePreferences(minimal);
    setShowBanner(false);
  };

  const savePreferences = (prefs: CookiePreferences) => {
    localStorage.setItem(COOKIE_CONSENT_KEY, 'true');
    localStorage.setItem(COOKIE_PREFERENCES_KEY, JSON.stringify(prefs));

    // Set essential cookies (always allowed)
    document.cookie = `bus-info-session=1; path=/; max-age=${60 * 60 * 24 * 30}; samesite=strict`;

    // Only set analytics cookies if user consented
    if (prefs.analytics) {
      // Analytics cookies will go here
      console.log('Analytics cookies enabled');
    } else {
      // Remove analytics cookies if they exist
      document.cookie = 'bus-info-analytics=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT';
    }

    // Dispatch custom event for other components to listen to
    window.dispatchEvent(
      new CustomEvent('cookiePreferencesChanged', {
        detail: prefs,
      }),
    );
  };

  const handlePreferenceChange = (type: keyof CookiePreferences, value: boolean) => {
    if (type === 'essential') return; // Cannot disable essential cookies

    setPreferences((prev) => ({
      ...prev,
      [type]: value,
    }));
  };

  if (!showBanner) return null;

  return (
    <div className={styles.cookieConsent}>
      <div className={styles.overlay} />
      <div className={styles.banner}>
        <div className={styles.content}>
          <div className={styles.header}>
            <h3>Cookie Notice</h3>
            <p>
              We use cookies and similar technologies to provide essential website functionality,
              improve your browsing experience, and analyze website usage in accordance with our
              Privacy Notice and UK GDPR requirements.
            </p>
          </div>

          {!showDetails ? (
            <div className={styles.basicView}>
              <p>
                Essential cookies are required for the website to function. You can accept all
                cookies or customize your preferences.
              </p>

              <div className={styles.actions}>
                <button onClick={handleAcceptAll} className={styles.acceptAll}>
                  Accept All
                </button>
                <button onClick={() => setShowDetails(true)} className={styles.customize}>
                  Customize
                </button>
                <button onClick={handleRejectAll} className={styles.rejectAll}>
                  Reject All
                </button>
              </div>
            </div>
          ) : (
            <div className={styles.detailView}>
              <div className={styles.cookieTypes}>
                <div className={styles.cookieType}>
                  <div className={styles.typeHeader}>
                    <input
                      type="checkbox"
                      id="essential"
                      checked={preferences.essential}
                      disabled
                    />
                    <label htmlFor="essential">
                      <strong>Essential Cookies</strong>
                    </label>
                  </div>
                  <p>
                    Required for basic website functionality including authentication, security, and
                    accessibility features. These cannot be disabled.
                  </p>
                </div>

                <div className={styles.cookieType}>
                  <div className={styles.typeHeader}>
                    <input
                      type="checkbox"
                      id="analytics"
                      checked={preferences.analytics}
                      onChange={(e) => handlePreferenceChange('analytics', e.target.checked)}
                    />
                    <label htmlFor="analytics">
                      <strong>Analytics Cookies</strong>
                    </label>
                  </div>
                  <p>
                    Help us understand how visitors interact with our website by collecting
                    anonymous information about usage patterns and performance.
                  </p>
                </div>
              </div>

              <div className={styles.actions}>
                <button onClick={handleAcceptSelected} className={styles.acceptSelected}>
                  Save Preferences
                </button>
                <button onClick={() => setShowDetails(false)} className={styles.back}>
                  Back
                </button>
              </div>
            </div>
          )}

          <div className={styles.links}>
            <a href="/privacy" target="_blank" rel="noopener noreferrer">
              Privacy Notice
            </a>
            <span>•</span>
            <a href="/terms" target="_blank" rel="noopener noreferrer">
              Terms of Service
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}

// Hook for other components to check cookie preferences
export function useCookiePreferences() {
  const [preferences, setPreferences] = useState<CookiePreferences>({
    essential: true,
    analytics: false,
    marketing: false,
  });

  useEffect(() => {
    // Load initial preferences
    const savedPreferences = localStorage.getItem(COOKIE_PREFERENCES_KEY);
    if (savedPreferences) {
      try {
        setPreferences(JSON.parse(savedPreferences));
      } catch (e) {
        console.error('Failed to parse cookie preferences:', e);
      }
    }

    // Listen for changes
    const handlePreferencesChange = (event: CustomEvent) => {
      setPreferences(event.detail);
    };

    window.addEventListener('cookiePreferencesChanged', handlePreferencesChange as EventListener);

    return () => {
      window.removeEventListener(
        'cookiePreferencesChanged',
        handlePreferencesChange as EventListener,
      );
    };
  }, []);

  return preferences;
}
