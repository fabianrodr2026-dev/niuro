import Link from "next/link";

export default function Denied() {
  return <main className="shell">
    <header className="topbar"><span className="brand">fundo<span className="dot">.</span></span><span className="topnote">Business financing, made simple</span></header>
    <section className="panel result">
      <div className="badge denied">Not approved</div>
      <h1>We can’t approve this application</h1>
      <p>Your application does not meet our current criteria.</p>
      <Link className="secondary link-button" href="/">Start another application</Link>
    </section>
    <footer>Fundo take-home project</footer>
  </main>;
}
