import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  // Identificativo soltanto per build locali. Prima dei record store va sostituito
  // con quello posseduto dall'organizzazione che pubblicherà PaghePro.
  appId: process.env.MOBILE_APP_ID ?? process.env.CAPACITOR_APP_ID ?? "it.paghepro.app.dev",
  appName: "PaghePro",
  webDir: "dist",
  bundledWebRuntime: false,
  server: {
    androidScheme: "https",
    cleartext: false,
  },
  ios: {
    contentInset: "automatic",
    preferredContentMode: "mobile",
  },
};

export default config;
