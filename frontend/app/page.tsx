"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

type Outcome = { approved: boolean; reason?: string; applicationId?: number; returningCustomer?: boolean };

export default function Home() {
  const router = useRouter();
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setLoading(true);
    const form = new FormData(event.currentTarget);
    const request = Object.fromEntries(form.entries());
    try {
      const response = await fetch("http://localhost:5050/api/applications", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...request, requestedAmount: Number(request.requestedAmount) })
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? "Application could not be submitted.");
      if (!result.approved) {
        router.push("/denied");
        return;
      }
      setOutcome(result);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Application could not be submitted.");
    } finally {
      setLoading(false);
    }
  }

  return <main className="shell">
    <header className="topbar"><span className="brand">fundo<span className="dot">.</span></span><span className="topnote">Business financing, made simple</span></header>
    <section className="panel">
      {outcome ? <div className="result">
        <div className="badge approved">Approved</div>
        <h1>Your application is approved</h1>
        <p>{outcome.returningCustomer ? "Your existing application has been updated." : "Your application has been saved."}</p>
        {outcome.applicationId && <p className="reference">Application #{outcome.applicationId}</p>}
        <button type="button" className="secondary" onClick={() => setOutcome(null)}>Start another application</button>
      </div> : <>
        <div className="intro"><div className="eyebrow">BUSINESS LOAN APPLICATION</div><h1>Tell us about your business</h1><p>Enter your details to receive an immediate decision.</p></div>
        <form onSubmit={submit}>
          <div className="grid">
            <label>First name<input name="firstName" autoComplete="given-name" required /></label>
            <label>Last name<input name="lastName" autoComplete="family-name" required /></label>
            <label className="wide">Street address<input name="address" autoComplete="street-address" required /></label>
            <label>State<input name="state" maxLength={2} placeholder="CA" autoComplete="address-level1" required /></label>
            <label>Company name<input name="companyName" autoComplete="organization" required /></label>
            <label>Requested amount ($)<input name="requestedAmount" type="number" min="1" step="0.01" required /></label>
            <label>SSN<input name="ssn" inputMode="numeric" placeholder="123-45-6789" pattern="[0-9]{3}-[0-9]{2}-[0-9]{4}" title="Use 123-45-6789 format" required /></label>
          </div>
          {error && <p role="alert" className="error">{error}</p>}
          <button disabled={loading} className="primary">{loading ? "Submitting…" : "Submit application"}</button>
          <p className="fineprint">Demo application. Use sample data only.</p>
        </form>
      </>}
    </section>
    <footer>Fundo take-home project</footer>
  </main>;
}
