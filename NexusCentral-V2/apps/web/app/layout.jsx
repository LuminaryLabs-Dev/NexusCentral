import "./styles.css";

export const metadata = { title: "Nexus Central Web", description: "A visual automation workbench for connected business workflows" };

export default function RootLayout({ children }) {
  return <html lang="en"><body>{children}</body></html>;
}
