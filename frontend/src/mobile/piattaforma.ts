import { Capacitor } from "@capacitor/core";
import { Browser } from "@capacitor/browser";
import { Directory, Filesystem } from "@capacitor/filesystem";
import { Share } from "@capacitor/share";

export const buildMobile = import.meta.env.VITE_MOBILE_APP === "1";
export const piattaformaNativa = buildMobile && Capacitor.isNativePlatform();

function arrayBufferBase64(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binario = "";
  const blocco = 0x8000;
  for (let i = 0; i < bytes.length; i += blocco)
    binario += String.fromCharCode(...bytes.subarray(i, i + blocco));
  return btoa(binario);
}

/** Salva temporaneamente il documento e apre il pannello Condividi nativo. */
export async function condividiDocumento(blob: Blob, nomeFile: string): Promise<void> {
  const nomeSicuro = nomeFile.replace(/[^a-zA-Z0-9._-]/g, "-");
  const percorso = `condivisioni/${crypto.randomUUID()}-${nomeSicuro}`;
  await Filesystem.writeFile({
    path: percorso,
    data: arrayBufferBase64(await blob.arrayBuffer()),
    directory: Directory.Cache,
    recursive: true,
  });
  const uri = await Filesystem.getUri({ path: percorso, directory: Directory.Cache });
  try {
    await Share.share({ title: nomeFile, files: [uri.uri], dialogTitle: "Condividi documento" });
  } finally {
    await Filesystem.deleteFile({ path: percorso, directory: Directory.Cache }).catch(() => undefined);
  }
}

/** Apre un URL HTTPS pubblico nel browser nativo, mantenendo il ritorno all'app. */
export async function apriLinkEsterno(url: string): Promise<void> {
  const destinazione = new URL(url);
  if (destinazione.protocol !== "https:")
    throw new Error("Il collegamento esterno deve usare HTTPS.");
  if (piattaformaNativa) {
    await Browser.open({ url: destinazione.href, presentationStyle: "popover" });
    return;
  }
  window.open(destinazione.href, "_blank", "noopener,noreferrer");
}
