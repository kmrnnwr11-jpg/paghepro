import { access, mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { dirname } from "node:path";
import { validaAppIdStore } from "./mobile-config.mjs";

const nuovo = validaAppIdStore(process.env.MOBILE_APP_ID);
const gradlePath = "android/app/build.gradle";
const gradle = await readFile(gradlePath, "utf8");
const trovato = gradle.match(/applicationId\s+"([a-zA-Z0-9_.-]+)"/);
if (!trovato) throw new Error("applicationId Android non trovato.");
const corrente = trovato[1];

const pbxPath = "ios/App/App.xcodeproj/project.pbxproj";
const configPath = "capacitor.config.ts";
const javaVecchio = `android/app/src/main/java/${corrente.replaceAll(".", "/")}/MainActivity.java`;
const javaNuovo = `android/app/src/main/java/${nuovo.replaceAll(".", "/")}/MainActivity.java`;

const pbx = await readFile(pbxPath, "utf8");
const config = await readFile(configPath, "utf8");
const java = await readFile(javaVecchio, "utf8");
if (!pbx.includes(`PRODUCT_BUNDLE_IDENTIFIER = ${corrente};`)
  || !gradle.includes(`namespace = "${corrente}"`)
  || !java.includes(`package ${corrente};`))
  throw new Error("Gli identificativi nativi correnti non sono coerenti: finalizzazione interrotta.");

if (corrente === nuovo) {
  console.log(`Identificativo già finalizzato: ${nuovo}`);
  process.exit(0);
}

if (await access(javaNuovo).then(() => true).catch(() => false))
  throw new Error(`Il percorso Java di destinazione esiste già: ${javaNuovo}`);

await mkdir(dirname(javaNuovo), { recursive: true });
await writeFile(gradlePath, gradle.replaceAll(corrente, nuovo));
await writeFile(pbxPath, pbx.replaceAll(`PRODUCT_BUNDLE_IDENTIFIER = ${corrente};`, `PRODUCT_BUNDLE_IDENTIFIER = ${nuovo};`));
await writeFile(configPath, config.replaceAll(`"${corrente}"`, `"${nuovo}"`));
await writeFile(javaVecchio, java.replace(`package ${corrente};`, `package ${nuovo};`));
await rename(javaVecchio, javaNuovo);

console.log(`Identificativo mobile finalizzato: ${corrente} -> ${nuovo}`);
console.log("Controlla il diff e non creare i record store finché il titolare non ha approvato questo valore.");
