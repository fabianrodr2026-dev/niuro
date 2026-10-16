import type { Metadata } from "next";
import "./styles.css";

export const metadata: Metadata = {
  title: "Fundo | Apply",
  description: "A simple loan application"
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
