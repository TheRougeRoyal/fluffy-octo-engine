import React from 'react';

const LegalLayout = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <div className="min-h-screen bg-deep text-text p-8 md:p-16 font-sans">
    <div className="max-w-3xl mx-auto">
      <h1 className="text-3xl font-bold mb-8 text-white">{title}</h1>
      <div className="prose prose-invert max-w-none text-text-muted leading-relaxed space-y-6">
        {children}
      </div>
      <div className="mt-12 pt-8 border-t border-white/10">
        <p className="text-xs text-text-muted italic">
          Last updated: September 9, 2026
        </p>
      </div>
    </div>
  </div>
);

export default LegalLayout;
