'use client';

import { useState } from 'react';
import { useAuth } from '@/hooks/useAuth';
import { Button } from '@/components/ui';
import styles from './page.module.scss';

interface DataExportRequest {
  status: 'idle' | 'requesting' | 'processing' | 'ready' | 'error';
  message?: string;
  downloadUrl?: string;
}

interface AccountDeletion {
  status: 'idle' | 'confirming' | 'processing' | 'error';
  message?: string;
}

interface CookiePreferences {
  analytics: boolean;
}

export default function SettingsPage() {
  const { user, logout } = useAuth();
  const [activeTab, setActiveTab] = useState<'account' | 'privacy' | 'data'>('account');
  const [dataExport, setDataExport] = useState<DataExportRequest>({ status: 'idle' });
  const [accountDeletion, setAccountDeletion] = useState<AccountDeletion>({ status: 'idle' });
  const [cookiePreferences, setCookiePreferences] = useState<CookiePreferences>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('bus-info-cookie-preferences');
      if (saved) {
        try {
          const parsed = JSON.parse(saved);
          return {
            analytics: parsed.analytics || false,
          };
        } catch {
          return { analytics: false };
        }
      }
    }
    return { analytics: false };
  });

  if (!user) {
    return (
      <div className={styles.settingsPage}>
        <div className={styles.settingsContainer}>
          <div className={styles.notLoggedIn}>
            <h1>Settings</h1>
            <p>You need to be logged in to access settings.</p>
            <a href="/login" className={styles.loginLink}>
              Log In
            </a>
          </div>
        </div>
      </div>
    );
  }

  const handleDataExport = async () => {
    setDataExport({ status: 'requesting' });
    
    try {
      // In development, simulate the API call
      if (process.env.NODE_ENV === 'development') {
        // Simulate network delay
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        setDataExport({
          status: 'processing',
          message: 'Your data export has been requested. You will receive an email with a download link within 24 hours.',
        });
        return;
      }

      const response = await fetch('/api/user/export-data', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
      });

      if (response.ok) {
        setDataExport({
          status: 'processing',
          message: 'Your data export has been requested. You will receive an email with a download link within 24 hours.',
        });
      } else {
        throw new Error('Failed to request data export');
      }
    } catch {
      setDataExport({
        status: 'error',
        message: 'Failed to request data export. Please try again or contact support.',
      });
    }
  };

  const handleAccountDeletion = async () => {
    if (accountDeletion.status !== 'confirming') {
      setAccountDeletion({ status: 'confirming' });
      return;
    }

    setAccountDeletion({ status: 'processing' });

    try {
      // In development, simulate the API call
      if (process.env.NODE_ENV === 'development') {
        // Simulate network delay
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        // Simulate successful deletion
        alert('Account deleted successfully (dev mode)');
        logout();
        window.location.href = '/';
        return;
      }

      const response = await fetch('/api/user/delete-account', {
        method: 'DELETE',
        credentials: 'include',
      });

      if (response.ok) {
        // Log out and redirect
        logout();
        window.location.href = '/';
      } else {
        throw new Error('Failed to delete account');
      }
    } catch {
      setAccountDeletion({
        status: 'error',
        message: 'Failed to delete account. Please try again or contact support.',
      });
    }
  };

  const handleCookiePreferenceChange = (type: 'analytics', enabled: boolean) => {
    const newPrefs = { ...cookiePreferences, [type]: enabled };
    setCookiePreferences(newPrefs);

    // Update localStorage and cookies
    const prefsToSave = {
      essential: true,
      analytics: newPrefs.analytics,
    };

    localStorage.setItem('bus-info-cookie-preferences', JSON.stringify(prefsToSave));

    // Set/remove cookies based on preferences
    if (newPrefs.analytics) {
      console.log('Analytics cookies enabled');
    } else {
      document.cookie = 'bus-info-analytics=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT';
    }

    // Dispatch event for other components
    window.dispatchEvent(new CustomEvent('cookiePreferencesChanged', { 
      detail: prefsToSave 
    }));
  };

  const tabs = [
    { key: 'account', label: 'Account', icon: 'fas fa-user' },
    { key: 'privacy', label: 'Privacy', icon: 'fas fa-shield-alt' },
    { key: 'data', label: 'Your Data', icon: 'fas fa-download' }
  ] as const;

  return (
    <div className={styles.settingsPage}>
      <div className={styles.settingsContainer}>

        <div className={styles.settingsContent}>
          <nav className={styles.settingsTabs}>
            {tabs.map(tab => (
              <button
                key={tab.key}
                className={`${styles.tabButton} ${activeTab === tab.key ? styles.active : ''}`}
                onClick={() => setActiveTab(tab.key)}
              >
                <i className={tab.icon}></i>
                <span>{tab.label}</span>
              </button>
            ))}
          </nav>

          <div className={styles.settingsPanel}>
            {activeTab === 'account' && (
              <div className={styles.panelContent}>
                <h2 className={styles.panelTitle}>Account Information</h2>
                <p className={styles.panelDescription}>Your account details and basic information.</p>
                
                <div className={styles.accountInfo}>
                  <div className={styles.infoItem}>
                    <label>Email</label>
                    <span>{user.email}</span>
                  </div>
                  <div className={styles.infoItem}>
                    <label>Member since</label>
                    <span>{new Date(user.created_at).toLocaleDateString()}</span>
                  </div>
                </div>

                <div className={styles.accountActions}>
                  <h3>Account Actions</h3>
                  <div className={styles.dangerZone}>
                    <div className={styles.dangerItem}>
                      <div className={styles.dangerInfo}>
                        <h4>Delete Account</h4>
                        <p>Permanently delete your account and all associated data. This action cannot be undone.</p>
                      </div>
                      
                      {(accountDeletion.status === 'confirming' || accountDeletion.status === 'processing') ? (
                        <div className={styles.confirmationBox}>
                          <p><strong>Are you absolutely sure?</strong></p>
                          <p>This will permanently delete:</p>
                          <ul>
                            <li>Your account and login credentials</li>
                            <li>All saved bus preferences</li>
                            <li>Any usage history</li>
                          </ul>
                          <p>This action cannot be undone.</p>
                          
                          <div className={styles.confirmationActions}>
                            <Button 
                              onClick={handleAccountDeletion}
                              variant="danger"
                              disabled={accountDeletion.status === 'processing'}
                            >
                              {accountDeletion.status === 'processing' ? 'Deleting...' : 'Yes, Delete My Account'}
                            </Button>
                            <Button 
                              onClick={() => setAccountDeletion({ status: 'idle' })}
                              variant="secondary"
                            >
                              Cancel
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <Button 
                          onClick={handleAccountDeletion}
                          variant="danger"
                        >
                          Delete Account
                        </Button>
                      )}

                      {accountDeletion.message && (
                        <div className={`${styles.statusMessage} ${styles.error}`}>
                          {accountDeletion.message}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'privacy' && (
              <div className={styles.panelContent}>
                <h2 className={styles.panelTitle}>Privacy Settings</h2>
                <p className={styles.panelDescription}>Control your privacy and cookie preferences.</p>
                
                <div className={styles.privacySection}>
                  <h3>Cookie Preferences</h3>
                  <p>Control which cookies we can use on your device.</p>
                  
                  <div className={styles.cookieControls}>
                    <div className={styles.cookieItem}>
                      <div className={styles.cookieInfo}>
                        <strong>Essential Cookies</strong>
                        <p>Required for the website to function (login, security)</p>
                      </div>
                      <label className={styles.switch}>
                        <input type="checkbox" checked={true} disabled />
                        <span className={styles.slider}></span>
                      </label>
                    </div>

                    <div className={styles.cookieItem}>
                      <div className={styles.cookieInfo}>
                        <strong>Analytics Cookies</strong>
                        <p>Help us understand how you use the site</p>
                      </div>
                      <label className={styles.switch}>
                        <input 
                          type="checkbox" 
                          checked={cookiePreferences.analytics}
                          onChange={(e) => handleCookiePreferenceChange('analytics', e.target.checked)}
                        />
                        <span className={styles.slider}></span>
                      </label>
                    </div>

                  </div>
                </div>

                <div className={styles.privacySection}>
                  <h3>Privacy Resources</h3>
                  <div className={styles.privacyLinks}>
                    <a href="/privacy" className={styles.privacyLink}>
                      <i className="fas fa-file-alt"></i>
                      <span>Privacy Notice</span>
                    </a>
                    <a href="/terms" className={styles.privacyLink}>
                      <i className="fas fa-gavel"></i>
                      <span>Terms of Service</span>
                    </a>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'data' && (
              <div className={styles.panelContent}>
                <h2 className={styles.panelTitle}>Your Data</h2>
                <p className={styles.panelDescription}>Request a copy of your data or manage data-related requests.</p>
                
                <div className={styles.dataSection}>
                  <h3>Data Export</h3>
                  <p>Under UK GDPR, you have the right to access your personal data. Request a copy of all data we hold about you.</p>
                  
                  <div className={styles.actionItem}>
                    <div className={styles.actionInfo}>
                      <strong>Download Your Data</strong>
                      <p>Get a copy of all your personal data including:</p>
                      <ul>
                        <li>Account information (email, registration date)</li>
                        <li>Bus route preferences and settings</li>
                        <li>Usage history and analytics data</li>
                        <li>Cookie preferences</li>
                      </ul>
                    </div>
                    <Button 
                      onClick={handleDataExport}
                      disabled={dataExport.status === 'requesting' || dataExport.status === 'processing'}
                      variant="primary"
                    >
                      <i className="fas fa-download"></i>
                      {dataExport.status === 'requesting' ? 'Requesting...' : 'Request Data Export'}
                    </Button>
                  </div>

                  {dataExport.message && (
                    <div className={`${styles.statusMessage} ${
                      dataExport.status === 'error' ? styles.error : styles.info
                    }`}>
                      <i className={dataExport.status === 'error' ? 'fas fa-exclamation-triangle' : 'fas fa-info-circle'}></i>
                      {dataExport.message}
                    </div>
                  )}
                </div>

                <div className={styles.dataSection}>
                  <h3>Data Rights</h3>
                  <p>Under UK GDPR, you have several rights regarding your personal data:</p>
                  
                  <div className={styles.dataRights}>
                    <div className={styles.rightItem}>
                      <i className="fas fa-eye"></i>
                      <div>
                        <strong>Right to Access</strong>
                        <p>Request a copy of your data (use the export button above)</p>
                      </div>
                    </div>
                    <div className={styles.rightItem}>
                      <i className="fas fa-edit"></i>
                      <div>
                        <strong>Right to Rectification</strong>
                        <p>Request corrections to inaccurate data</p>
                      </div>
                    </div>
                    <div className={styles.rightItem}>
                      <i className="fas fa-trash"></i>
                      <div>
                        <strong>Right to Erasure</strong>
                        <p>Request deletion of your data (use account deletion)</p>
                      </div>
                    </div>
                    <div className={styles.rightItem}>
                      <i className="fas fa-ban"></i>
                      <div>
                        <strong>Right to Object</strong>
                        <p>Object to certain processing of your data</p>
                      </div>
                    </div>
                  </div>
                  
                  <p>To exercise any of these rights or if you have questions about your data, contact us at: 
                    <a href="mailto:support@konpeki.co.uk"> support@konpeki.co.uk</a>
                  </p>
                  <p>We&apos;ll respond within one month as required by UK GDPR.</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}