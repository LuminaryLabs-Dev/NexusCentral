import "./styles.css";

export const metadata = { title: "Nexus Central Web", description: "Shared LAN coding workspace" };

export default function RootLayout({ children }) {
  return <html lang="en"><body>{children}</body></html>;
}
