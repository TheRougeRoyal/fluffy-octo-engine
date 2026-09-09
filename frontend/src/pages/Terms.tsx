import LegalLayout from './LegalLayout';

const TermsOfService = () => (
  <LegalLayout title="Terms of Service">
    <section>
      <h2 className="text-xl font-semibold text-white mb-2">1. Demo Nature of Service</h2>
      <p>
        <strong>DISCLAIMER:</strong> Fluffy Octo is a demonstration/paper-trading system. It is not a brokerage,
        financial services product, or investment advisory service. No real capital is at risk, and no real
        trades are executed on any live exchange.
      </p>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">2. Use of Service</h2>
      <p>
        By using this terminal, you agree to use the system for educational and testing purposes only.
        The system utilizes a simulated matching engine to provide a realistic trading experience.
      </p>
    </section>

    <section>
      <h2 className="text-xl font-semibold text-white mb-2">3. Limitation of Liability</h2>
      <p>
        The service is provided "as-is". The developers are not responsible for any perceived financial
        loss or data errors occurring within the simulated environment.
      </p>
    </section>
  </LegalLayout>
);

export default TermsOfService;
