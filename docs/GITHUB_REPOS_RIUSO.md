# Riuso GitHub per Paghe SaaS

Ricerca fatta il 2026-06-26 su repository GitHub pubblici. Obiettivo: trovare componenti
riusabili per rafforzare il software paghe senza importare codice con licenze rischiose o
progetti poco mantenuti.

## Gia integrato nel progetto

| Area | Repository | Licenza | Uso nel progetto | Nota |
|---|---|---:|---|---|
| PDF cedolino/fattura | [QuestPDF/QuestPDF](https://github.com/QuestPDF/QuestPDF) | Community / commerciale | `backend/src/Paghe.Documenti` | Ottimo per layout PDF. Prima della produzione verificare se si rientra nella Community License o serve licenza paid. |

## Firma digitale (CAdES .p7m + CNS/smart card) — valutato e integrato il 2026-06-26

Obiettivo: firma CAdES-BES (`.xml.p7m`) per la fattura elettronica e firma con
**CNS / smart card** del titolare. Modulo `backend/src/Paghe.Firma`.

| Componente | Repository / fonte | Licenza | Decisione |
|---|---|---:|---|
| Engine CAdES/CMS | `System.Security.Cryptography.Pkcs` (`SignedCms`) | runtime .NET (MIT) | ✅ **Usato.** Base della busta `.p7m` con digest SHA-256 e attributo `SigningCertificateV2`. Nessuna dipendenza esterna rischiosa. |
| Accesso CNS/smart card | [Pkcs11Interop](https://github.com/Pkcs11Interop/Pkcs11Interop) + [Pkcs11Interop.X509Store](https://github.com/Pkcs11Interop/Pkcs11Interop.X509Store) | **Apache-2.0** | ✅ **Usato** per `FirmatoreCns`. Parla con il middleware PKCS#11 della carta; la chiave privata resta sul chip. Si integra con `SignedCms`. |
| Firma XAdES/CAdES alternativa | [ctt-gob-es/FirmaXadesNet](https://github.com/ctt-gob-es/FirmaXadesNet) / [FirmaCadesNet](https://github.com/ctt-gob-es/FirmaCadesNet) e fork .NET Core | **GPL-3.0** | ❌ **Scartata.** Copyleft forte: vietata nel SaaS senza decisione legale esplicita (vedi Regola di sicurezza). |

> Nota CNS: la firma con smart card richiede il middleware PKCS#11 (file `.so`/`.dll`/`.dylib`),
> il lettore e la carta **sulla macchina che firma**. Funziona in contesto desktop o tramite
> agente locale, non in un backend cloud dove la carta è assente: in SaaS la firma lato server
> si fa con certificato PFX o firma remota (QTSP/HSM).

## Da integrare con priorita alta

| Area | Repository | Licenza | Decisione |
|---|---|---:|---|
| FatturaPA completa | [FatturaElettronica/FatturaElettronica.NET](https://github.com/FatturaElettronica/FatturaElettronica.NET) | BSD-like | Sostituire o affiancare il generatore XML subset, aggiungendo validazione completa e modelli ufficiali. |
| API docs | [domaindrivendev/Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) | MIT | Aggiungere Swagger UI/OpenAPI per testare gli endpoint, inclusi download PDF/XML. |
| Validazione input | [FluentValidation/FluentValidation](https://github.com/FluentValidation/FluentValidation) | Apache-2.0 | Centralizzare regole per DTO, anagrafiche, fatture e cartellini. |
| Multi-tenant hardening | [Finbuckle/Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant) | Apache-2.0 | Valutare quando il tenant non sara piu solo header demo. Utile con store tenant e strategie ASP.NET Core. |

## Da integrare dopo l'MVP

| Area | Repository | Licenza | Decisione |
|---|---|---:|---|
| Export Excel | [ClosedXML/ClosedXML](https://github.com/ClosedXML/ClosedXML) | MIT | Esportare cedolini, prima nota, fatture e riepiloghi mensili in `.xlsx`. |
| Job schedulati | [quartznet/quartznet](https://github.com/quartznet/quartznet) | Apache-2.0 | Batch mensili: generazione massiva cedolini, LUL, UniEmens, F24, CU. |
| Object storage | [minio/minio-dotnet](https://github.com/minio/minio-dotnet) | Apache-2.0 | Salvare PDF/XML su storage S3-compatibile con hash e metadata in tabella `documento`. |
| Calendari/festivita | [nager/Nager.Date](https://github.com/nager/Nager.Date) | MIT repo, NuGet/offline da verificare | Utile per festivita e giorni lavorativi. Il README indica licenza key per uso NuGet/offline: verificare prima di aggiungerlo come dipendenza. |
| PDF MIT fallback | [ststeiger/PdfSharpCore](https://github.com/ststeiger/PdfSharpCore) | MIT | Piano B se la licenza QuestPDF non e adatta al profilo commerciale. Layout piu manuale ma licenza semplice. |

## Solo come riferimento, non copiare

| Area | Repository | Licenza | Motivo |
|---|---|---:|---|
| Fisco/benefit Italia | [openfisca/openfisca-italy](https://github.com/openfisca/openfisca-italy) | AGPL-3.0 | Buono per orientamento concettuale, ma in un SaaS l'AGPL puo imporre obblighi forti sul sorgente. Non incorporare senza decisione legale. |
| UniEmens community | [ricscan/UniEmensExplorer](https://github.com/ricscan/UniEmensExplorer) e repo simili | Non chiara | Pochi segnali di manutenzione/licenza. Meglio implementare da specifiche INPS ufficiali o da libreria commerciale/validata. |

## Prossimo ordine consigliato

1. Aggiungere Swagger/OpenAPI per rendere provabili gli endpoint gia presenti.
2. Integrare FatturaElettronica.NET per validazione FatturaPA completa.
3. Sostituire i seed normativi con tabelle versionate e import controllato.
4. Aggiungere storage documenti con hash SHA-256 e metadati.
5. Preparare job mensili per cedolini/LUL/adempimenti.

## Regola di sicurezza

Non copiare codice da repository senza licenza chiara. Per librerie con copyleft forte
AGPL/GPL, usare solo come studio o riferimento esterno finche non esiste una scelta legale
esplicita.
