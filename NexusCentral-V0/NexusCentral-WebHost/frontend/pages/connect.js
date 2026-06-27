import Head from "next/head";
import Script from "next/script";

export default function ConnectPage() {
  return (
    <>
      <Head>
        <title>Nexus Web Lab Connection</title>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </Head>
      <div id="connectApp" className="connect-shell">
        <header className="topbar">
          <div>
            <div className="eyebrow">Secure Connection</div>
            <h1>Nexus Web Lab</h1>
          </div>
          <a className="back-link" href="/">
            Back to Lab
          </a>
        </header>
        <main className="connect-main">
          <section className="connect-card">
            <div className="section-title">Connection Status</div>
            <div id="connectPageState" className="connect-state loading">
              Loading connection status…
            </div>
            <div id="connectPageDetails" className="connect-details"></div>
          </section>
        </main>
      </div>
      <Script src="/connect.js" strategy="afterInteractive" />
    </>
  );
}
