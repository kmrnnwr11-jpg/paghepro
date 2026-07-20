/** Valida i due valori irreversibili usati per preparare i record negli store. */
export function validaAppIdStore(appId) {
  const identificativo = (appId ?? "").trim().toLowerCase();
  if (!/^[a-z][a-z0-9]*(\.[a-z0-9][a-z0-9-]*){2,}$/.test(identificativo))
    throw new Error("MOBILE_APP_ID non valido. Usa almeno tre parti, per esempio it.azienda.paghepro.");
  if (identificativo.endsWith(".dev"))
    throw new Error("MOBILE_APP_ID termina con .dev: per gli store serve l'identificativo definitivo.");

  return identificativo;
}

export function validaConfigurazioneMobile(apiUrl, appId) {
  const indirizzo = (apiUrl ?? "").trim();
  if (!indirizzo)
    throw new Error("MOBILE_API_URL mancante. Indica l'API pubblica HTTPS prima della build mobile.");

  let api;
  try {
    api = new URL(indirizzo);
  } catch {
    throw new Error("MOBILE_API_URL non è un URL valido.");
  }

  if (api.protocol !== "https:" || api.hostname.endsWith(".invalid"))
    throw new Error("La build mobile richiede un'API HTTPS reale; URL HTTP o .invalid non ammessi.");

  const identificativo = validaAppIdStore(appId);

  return { apiOrigin: api.origin, appId: identificativo };
}
