import { spawnSync } from "node:child_process";
import { validaConfigurazioneMobile } from "./mobile-config.mjs";

try {
  const configurazione = validaConfigurazioneMobile(
    process.env.MOBILE_API_URL,
    process.env.MOBILE_APP_ID,
  );
  process.env.VITE_API_URL = configurazione.apiOrigin;
  process.env.VITE_MOBILE_APP = "1";
  process.env.CAPACITOR_APP_ID = configurazione.appId;
} catch (errore) {
  console.error(errore instanceof Error ? errore.message : String(errore));
  process.exit(2);
}

const risultato = spawnSync("npm", ["run", "build"], {
  stdio: "inherit",
  env: process.env,
});
process.exit(risultato.status ?? 1);
