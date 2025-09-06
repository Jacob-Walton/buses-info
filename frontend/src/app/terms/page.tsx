import { Metadata } from 'next';
import styles from './page.module.scss';

export const metadata: Metadata = {
  title: 'Terms of Service - Bus Info',
  description: 'Terms of Service for using our bus information service.',
};

export default function TermsPage() {
  return (
    <div className={styles.contentPage}>
      <div className={styles.pageContainer}>
        <div className={styles.pageContent}>
          <div className={styles.pageHeader}>
            <h1>Terms of Service</h1>
            <div className={styles.lastUpdated}>Effective Date: 17 August 2025</div>
          </div>

          <div className={styles.contentSection} id="acceptance">
            <h2>1. Agreement</h2>
            <p>
              By using Bus Info, you agree to these terms. If you don&apos;t agree, please
              don&apos;t use the service.
            </p>
          </div>

          <div className={styles.contentSection} id="service">
            <h2>2. What This Service Does</h2>
            <p>
              Bus Info provides bus arrival information for informational purposes only. The
              information comes from third-party sources and may not always be accurate or
              up-to-date.
            </p>
            <p>
              <strong>Important:</strong> Don&apos;t rely solely on this service for important
              decisions. Always check official sources.
            </p>
          </div>

          <div className={styles.contentSection} id="accounts">
            <h2>3. User Accounts</h2>
            <p>If you create an account:</p>
            <ul>
              <li>Provide accurate information</li>
              <li>Keep your password secure</li>
              <li>Don&apos;t share your account with others</li>
              <li>Let us know if someone else accesses your account</li>
            </ul>
            <p>You must be at least 16 years old to create an account.</p>
          </div>

          <div className={styles.contentSection} id="acceptable-use">
            <h2>4. How to Use This Service</h2>
            <p>Please use the service responsibly. Don&apos;t:</p>
            <ul>
              <li>Try to break or hack the website</li>
              <li>Use automated tools to scrape data</li>
              <li>Do anything illegal</li>
              <li>Interfere with other users</li>
              <li>Use the service for commercial purposes without permission</li>
            </ul>
          </div>

          <div className={styles.contentSection} id="availability">
            <h2>5. Service Availability</h2>
            <p>We try to keep the service running, but sometimes it might be unavailable due to:</p>
            <ul>
              <li>Maintenance</li>
              <li>Technical problems</li>
              <li>Issues with data sources</li>
            </ul>
            <p>We don&apos;t guarantee the service will always be available.</p>
          </div>

          <div className={styles.contentSection} id="accuracy">
            <h2>6. Data Accuracy</h2>
            <p>Bus information comes from external sources and may be affected by:</p>
            <ul>
              <li>Traffic and weather conditions</li>
              <li>Service disruptions</li>
              <li>Schedule changes</li>
              <li>Technical issues</li>
            </ul>
            <p>We can&apos;t guarantee the information is always correct or complete.</p>
          </div>

          <div className={styles.contentSection} id="intellectual-property">
            <h2>7. Ownership</h2>
            <p>
              The website design and code belong to us. You can use the service for personal use but
              can&apos;t copy, modify, or distribute our content without permission.
            </p>
          </div>

          <div className={styles.contentSection} id="privacy">
            <h2>8. Privacy</h2>
            <p>
              How we handle your personal information is explained in our{' '}
              <a href="/privacy">Privacy Notice</a>.
            </p>
          </div>

          <div className={styles.contentSection} id="disclaimers">
            <h2>9. Disclaimers</h2>
            <p>
              This service is provided &quot;as is&quot; without warranties. We can&apos;t promise
              that:
            </p>
            <ul>
              <li>The service will always work perfectly</li>
              <li>The information will always be accurate</li>
              <li>The service will be uninterrupted</li>
              <li>All bugs will be fixed immediately</li>
            </ul>
          </div>

          <div className={styles.contentSection} id="liability">
            <h2>10. Liability</h2>
            <p>
              We&apos;re not responsible for any problems that arise from using this service,
              including:
            </p>
            <ul>
              <li>Missed buses or connections</li>
              <li>Inaccurate information</li>
              <li>Service downtime</li>
              <li>Any inconvenience or costs you might incur</li>
            </ul>
            <p>Use the service at your own risk.</p>
          </div>

          <div className={styles.contentSection} id="termination">
            <h2>11. Account Termination</h2>
            <p>
              You can delete your account anytime. We can also suspend or terminate accounts that
              violate these terms.
            </p>
          </div>

          <div className={styles.contentSection} id="changes">
            <h2>12. Changes to These Terms</h2>
            <p>
              We may update these terms occasionally. If we make significant changes, we&apos;ll let
              you know through the website or by email (if you have an account).
            </p>
          </div>

          <div className={styles.contentSection} id="governing-law">
            <h2>13. Legal Stuff</h2>
            <p>
              These terms are governed by the laws of England and Wales. Any disputes will be
              handled in English courts.
            </p>
          </div>

          <div className={styles.contentSection} id="contact">
            <h2>14. Contact</h2>
            <p>Questions about these terms? Contact us at: support@konpeki.co.uk</p>
          </div>
        </div>
      </div>
    </div>
  );
}
