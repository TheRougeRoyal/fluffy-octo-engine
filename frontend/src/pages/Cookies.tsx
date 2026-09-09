import LegalLayout from './LegalLayout';

const CookiePolicy = () => (
  <LegalLayout title="Cookie Policy">
    <section>
      <h2 className="text-xl font-semibold text-white mb-2">1. Use of Local Storage</h2>
      <p>
        Fluffy Octo uses browser local storage and session cookies primarily for
        authentication management through Firebase. This ensures you remain signed in
        across browser refreshes without re-entering credentials.
      </p>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">2. Essential Cookies</h2>
      <p>
        We only utilize "strictly necessary" cookies. These are essential for the basic
        functionality of the trading terminal and do not track your behavior across
        other websites.
      </p>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">3. Managing Your Preferences</h2>
      <p>
        You can clear your site data or sign out of the application at any time to
        remove authentication tokens from your browser.
      </p>
    </section>
  </LegalLayout>
);

export default CookiePolicy;
