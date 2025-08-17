import { Metadata } from 'next';
import styles from './page.module.scss';

export const metadata: Metadata = {
  title: 'Privacy Notice - Bus Info',
  description: 'Privacy Notice explaining how we handle your data when using our bus information service.',
};

export default function PrivacyPage() {
  return (
    <div className={styles.contentPage}>
      <div className={styles.pageContainer}>
        <div className={styles.pageContent}>
          <div className={styles.pageHeader}>
            <h1>Privacy Notice</h1>
            <div className={styles.lastUpdated}>Effective Date: 17 August 2025</div>
          </div>

          <div className={styles.contentSection} id="controller">
            <h2>1. Who We Are</h2>
            <p>Bus Info is operated by an individual developer. This Privacy Notice explains how we handle your personal data when you use our bus information service.</p>
            <p>Contact: support@konpeki.co.uk</p>
          </div>

          <div className={styles.contentSection} id="what-we-collect">
            <h2>2. What Information We Collect</h2>
            
            <h3>Account Information (if you register)</h3>
            <ul>
              <li>Email address</li>
              <li>Password (encrypted)</li>
              <li>Your bus route preferences</li>
            </ul>

            <h3>Automatically Collected</h3>
            <ul>
              <li>Basic technical information (IP address, browser type) for security</li>
              <li>Which pages you visit to understand how the site is used</li>
              <li>Session cookies to keep you logged in</li>
            </ul>

            <p>We do not collect sensitive personal data.</p>
          </div>

          <div className={styles.contentSection} id="why-we-collect">
            <h2>3. Why We Collect This Information</h2>
            <ul>
              <li><strong>Account features:</strong> To save your bus preferences and provide a personalized experience</li>
              <li><strong>Service operation:</strong> To keep the website running and secure</li>
              <li><strong>Improvement:</strong> To understand which features are most useful</li>
            </ul>
          </div>

          <div className={styles.contentSection} id="legal-basis">
            <h2>4. Legal Basis</h2>
            <ul>
              <li><strong>Account features:</strong> Performance of contract (you agree to our terms when registering)</li>
              <li><strong>Security and analytics:</strong> Legitimate interests (keeping the service secure and improving it)</li>
              <li><strong>Marketing cookies:</strong> Your consent (if you choose to accept them)</li>
            </ul>
          </div>

          <div className={styles.contentSection} id="data-sharing">
            <h2>5. Sharing Your Information</h2>
            <p>We do not sell or share your personal information with third parties, except:</p>
            <ul>
              <li><strong>Service providers:</strong> Cloud hosting to keep the website running</li>
              <li><strong>Legal requirements:</strong> If required by law</li>
            </ul>
            <p>The website is hosted in the UK/EU with appropriate data protection.</p>
          </div>

          <div className={styles.contentSection} id="retention">
            <h2>6. How Long We Keep Your Data</h2>
            <ul>
              <li><strong>Account data:</strong> Until you delete your account, then 30 days</li>
              <li><strong>Analytics data:</strong> 2 years, then anonymized</li>
              <li><strong>Security logs:</strong> 1 year</li>
            </ul>
          </div>

          <div className={styles.contentSection} id="your-rights">
            <h2>7. Your Rights</h2>
            <p>Under UK GDPR, you can:</p>
            <ul>
              <li><strong>Access:</strong> Request a copy of your data</li>
              <li><strong>Correct:</strong> Fix any incorrect information</li>
              <li><strong>Delete:</strong> Remove your account and data</li>
              <li><strong>Object:</strong> Stop certain uses of your data</li>
              <li><strong>Portability:</strong> Get your data in a standard format</li>
            </ul>
            <p>Contact support@konpeki.co.uk to exercise these rights. We&apos;ll respond within one month.</p>
          </div>

          <div className={styles.contentSection} id="cookies">
            <h2>8. Cookies</h2>
            <p>We use cookies for:</p>
            <ul>
              <li><strong>Essential:</strong> Keeping you logged in (required for the site to work)</li>
              <li><strong>Analytics:</strong> Understanding how the site is used (optional)</li>
              <li><strong>Preferences:</strong> Remembering your settings (optional)</li>
            </ul>
            <p>You can control non-essential cookies through the cookie banner or your browser settings.</p>
          </div>

          <div className={styles.contentSection} id="security">
            <h2>9. Security</h2>
            <p>We take reasonable steps to protect your information, including:</p>
            <ul>
              <li>Encrypted passwords</li>
              <li>Secure hosting</li>
              <li>Regular security updates</li>
            </ul>
            <p>However, no internet service is 100% secure. We cannot guarantee absolute security.</p>
          </div>

          <div className={styles.contentSection} id="children">
            <h2>10. Children</h2>
            <p>Our service is not intended for children under 16. We do not knowingly collect information from children under 16.</p>
          </div>

          <div className={styles.contentSection} id="changes">
            <h2>11. Changes to This Notice</h2>
            <p>We may update this Privacy Notice occasionally. If we make significant changes, we&apos;ll notify you via email (if you have an account) or through the website.</p>
          </div>

          <div className={styles.contentSection} id="complaints">
            <h2>12. Complaints</h2>
            <p>If you&apos;re not happy with how we&apos;ve handled your data, you can contact the Information Commissioner&apos;s Office (ICO):</p>
            <p>Website: ico.org.uk<br/>
            Telephone: 0303 123 1113</p>
          </div>

          <div className={styles.contentSection} id="contact">
            <h2>13. Contact</h2>
            <p>For questions about this Privacy Notice or your data:</p>
            <p><strong>Email:</strong> support@konpeki.co.uk</p>
          </div>
        </div>
      </div>
    </div>
  );
}