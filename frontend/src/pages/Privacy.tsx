import LegalLayout from './LegalLayout';

const PrivacyPolicy = () => (
  <LegalLayout title="Privacy Policy">
    <section>
      <h2 className="text-xl font-semibold text-white mb-2">1. Data Collection</h2>
      <p>
        We collect the following information to facilitate your trading session:
      </p>
      <ul className="list-disc pl-6 space-y-2">
        <li><strong>Authentication Data:</strong> Email addresses and account IDs managed via Firebase Authentication.</li>
        <li><strong>Activity Logs:</strong> A record of your simulated orders and system events stored in Firestore.</li>
        <li><strong>Session State:</strong> User preferences and temporary state stored in your browser's local storage.</li>
      </ul>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">2. How We Use Data</h2>
      <p>
        Collected data is used exclusively to:
      </p>
      <ul className="list-disc pl-6 space-y-2">
        <li>Maintain your paper-trading portfolio and order history.</li>
        <li>Authenticate your identity upon returning to the terminal.</li>
        <li>Improve the simulation engine through activity analysis.</li>
      </ul>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">3. Third-Party Services</h2>
      <p>
        We use Google Firebase for authentication and database storage. Your data is subject to
        the Google Firebase Privacy Policy. We do not sell or trade your data with third parties.
      </p>
    </section>
  </LegalLayout>
);

export default PrivacyPolicy;
