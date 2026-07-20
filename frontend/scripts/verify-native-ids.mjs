import { readFile } from "node:fs/promises";
import { validaAppIdStore } from "./mobile-config.mjs";

const atteso = validaAppIdStore(process.env.MOBILE_APP_ID);
const controlli = [
  ["Android applicationId", "android/app/build.gradle", `applicationId "${atteso}"`],
  ["Android namespace", "android/app/build.gradle", `namespace = "${atteso}"`],
  ["Android package Java", `android/app/src/main/java/${atteso.replaceAll(".", "/")}/MainActivity.java`, `package ${atteso};`],
  ["iOS bundle ID", "ios/App/App.xcodeproj/project.pbxproj", `PRODUCT_BUNDLE_IDENTIFIER = ${atteso};`],
];

for (const [etichetta, percorso, frammento] of controlli) {
  const contenuto = await readFile(percorso, "utf8").catch(() => "");
  if (!contenuto.includes(frammento)) {
    console.error(`${etichetta} non coincide con ${atteso}. Esegui prima npm run mobile:finalize-id.`);
    process.exit(2);
  }
}

console.log(`Identificativi nativi verificati: ${atteso}`);
