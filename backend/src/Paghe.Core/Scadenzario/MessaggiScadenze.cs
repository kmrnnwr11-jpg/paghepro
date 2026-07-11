using System.Globalization;
using Paghe.Core.Notifiche;

namespace Paghe.Core.Scadenzario;

/// <summary>
/// Genera i messaggi di avviso precompilati per ogni tipo di scadenza fiscale/contributiva.
/// Sostituisce le variabili {azienda}, {data}, {importo}, {numero}, {periodo}, {studio}.
/// </summary>
public static class MessaggiScadenze
{
    public static MessaggioNotifica Crea(
        string codiceTipo,
        string nomeAzienda,
        DateOnly dataScadenza,
        string? nomeStudio = null,
        string? importo = null,
        string? numero = null,
        string? periodo = null)
    {
        string studio = nomeStudio ?? "Studio";
        string data = dataScadenza.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string imp = importo ?? "da definire";
        string per = periodo ?? dataScadenza.ToString("MM/yyyy", CultureInfo.InvariantCulture);

        var (oggetto, corpo, breve) = codiceTipo switch
        {
            "DURC" => (
                $"Scadenza DURC – {nomeAzienda} – {data}",
                $"""
                Gentile cliente,

                il DURC dell'azienda {nomeAzienda}{(numero is not null ? $" (n. {numero})" : "")} scade il {data}.

                Trascorsa questa data il documento non sarà più valido per gare d'appalto,
                accesso a contributi pubblici e cessione crediti.

                Azione richiesta: richiedere il rinnovo tramite il portale INPS/INAIL
                (https://www.sportellounicoprevidenziale.it).

                Cordiali saluti,
                {studio}
                """,
                $"⚠️ DURC {nomeAzienda} scade il {data}. Rinnovare tramite portale INPS/INAIL. — {studio}"),

            "F24Irpef" => (
                $"F24 ritenute IRPEF – {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare le ritenute IRPEF trattenute ai
                dipendenti nel mese precedente.

                Importo stimato: € {imp}
                Codice tributo principale: 1001 (lavoro dipendente)

                Il modello F24 è disponibile nell'area riservata del gestionale.

                Cordiali saluti,
                {studio}
                """,
                $"📅 F24 ritenute IRPEF {nomeAzienda}: scadenza {data}, imp. € {imp}. — {studio}"),

            "F24Inps" => (
                $"F24 contributi INPS – {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare i contributi INPS dei dipendenti.

                Importo stimato: € {imp}
                Codici tributo: 0001 (quota lavoratore) + 0016 (quota datore)

                Il modello F24 è disponibile nell'area riservata.

                Cordiali saluti,
                {studio}
                """,
                $"📅 INPS dipendenti {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "UniEmens" => (
                $"UniEmens INPS – {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} deve essere inviato il flusso UniEmens all'INPS
                relativo al mese precedente.

                Vi chiediamo di confermare i cedolini del periodo per procedere all'invio.

                Cordiali saluti,
                {studio}
                """,
                $"📅 UniEmens {nomeAzienda}: scadenza invio {data}. — {studio}"),

            "IvaMensile" or "IvaTrimestrale" => (
                $"Liquidazione IVA {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare l'IVA a debito del periodo {per}.

                Importo da versare: € {imp}
                (IVA vendite – IVA acquisti. Se a credito non è necessario alcun versamento.)

                Codice tributo: 6001–6012 (mensile) / 6031–6033 (trimestrale)

                Cordiali saluti,
                {studio}
                """,
                $"📅 IVA {per} {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "Lipe" => (
                $"LIPE – Liquidazione Periodica IVA {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} deve essere trasmessa all'Agenzia delle Entrate la
                Comunicazione delle Liquidazioni Periodiche IVA (LIPE) relativa al periodo {per}.

                La comunicazione viene inviata telematicamente dallo studio.
                Vi chiediamo di confermare che tutte le fatture del periodo siano complete.

                Cordiali saluti,
                {studio}
                """,
                $"📅 LIPE {per} {nomeAzienda}: scadenza trasmissione {data}. — {studio}"),

            "InpsArtigiani" => (
                $"Contributi INPS fissi {per} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare la rata trimestrale dei contributi
                INPS fissi (quota minimale) tramite modello F24.

                Importo rata: € {imp}

                Cordiali saluti,
                {studio}
                """,
                $"📅 INPS artigiani/commercianti {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "InailSaldo" or "InailAcconto" => (
                $"Autoliquidazione INAIL {(codiceTipo == "InailSaldo" ? "saldo" : "acconto")} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare {(codiceTipo == "InailSaldo"
                    ? "il saldo INAIL dell'anno precedente e il primo acconto dell'anno corrente"
                    : "il secondo acconto INAIL dell'anno corrente")}.

                Importo stimato: € {imp}

                È necessario anche inviare la dichiarazione delle retribuzioni (Modello 1031)
                se non già trasmessa.

                Cordiali saluti,
                {studio}
                """,
                $"📅 INAIL {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "CuLavoratori" => (
                $"Certificazione Unica – consegna lavoratori entro {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario consegnare ai dipendenti la Certificazione
                Unica con i redditi da lavoro dell'anno precedente.

                Lo studio provvederà alla trasmissione telematica all'AdE entro il 16 marzo.

                Vi chiediamo di confermare l'elenco dei dipendenti in forza al 31/12.

                Cordiali saluti,
                {studio}
                """,
                $"📅 CU lavoratori {nomeAzienda}: consegna entro {data}. — {studio}"),

            "CuAde" => (
                $"CU telematica all'AdE – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} lo studio trasmetterà telematicamente all'Agenzia delle Entrate
                le Certificazioni Uniche dei vostri dipendenti/collaboratori.

                Azione richiesta: confermare che i dati siano completi e corretti.

                Cordiali saluti,
                {studio}
                """,
                $"📅 CU telematica AdE {nomeAzienda}: trasmissione entro {data}. — {studio}"),

            "DichIva" => (
                $"Dichiarazione IVA annuale – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} deve essere presentata telematicamente la Dichiarazione IVA
                relativa all'anno precedente.

                Per predisporre la dichiarazione occorrono:
                – Registro acquisti e vendite completo
                – Eventuali note di variazione non ancora registrate

                Vi preghiamo di inviarci la documentazione entro 10 giorni.

                Cordiali saluti,
                {studio}
                """,
                $"📅 Dich. IVA {nomeAzienda}: presentare entro {data}. — {studio}"),

            "SaldoIrpef" => (
                $"Saldo IRPEF + 1° acconto – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare il saldo IRPEF dell'anno precedente
                e il primo acconto IRPEF dell'anno corrente.

                Importo stimato: € {imp}

                Il modello F24 è predisposto dallo studio.

                Cordiali saluti,
                {studio}
                """,
                $"📅 Saldo+acconto IRPEF {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "ImuAcconto" or "ImuSaldo" => (
                $"IMU {(codiceTipo == "ImuAcconto" ? "acconto" : "saldo")} – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare l'{(codiceTipo == "ImuAcconto" ? "acconto" : "saldo")} IMU
                per gli immobili di proprietà.

                Importo stimato: € {imp}

                Il modello F24 è predisposto dallo studio.

                Cordiali saluti,
                {studio}
                """,
                $"📅 IMU {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "Modello770" => (
                $"Modello 770 – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} deve essere trasmesso il Modello 770 all'Agenzia delle Entrate
                con i dati delle ritenute operate nell'anno precedente.

                Vi chiediamo di confermare che i dati siano completi.

                Cordiali saluti,
                {studio}
                """,
                $"📅 Modello 770 {nomeAzienda}: trasmettere entro {data}. — {studio}"),

            "ModelliRedditi" => (
                $"Modello Redditi – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario presentare il Modello Redditi relativo
                all'anno d'imposta precedente.

                Per completare la pratica occorrono:
                – Documenti spese deducibili/detraibili
                – Dati immobili in proprietà o locazione
                – Altri redditi percepiti

                Vi invitiamo a fissare un appuntamento con lo studio.

                Cordiali saluti,
                {studio}
                """,
                $"📅 Modello Redditi {nomeAzienda}: presentare entro {data}. — {studio}"),

            "Acconto2Irpef" => (
                $"2° acconto IRPEF – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare il 2° acconto IRPEF.

                Importo stimato: € {imp}

                Il modello F24 è predisposto dallo studio.

                Cordiali saluti,
                {studio}
                """,
                $"📅 2° acconto IRPEF {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            "Acconto2Inps" => (
                $"2° acconto INPS artigiani/commercianti – scadenza {data}",
                $"""
                Gentile cliente,

                entro il {data} è necessario versare il 2° acconto INPS.

                Importo stimato: € {imp}

                Cordiali saluti,
                {studio}
                """,
                $"📅 2° acconto INPS {nomeAzienda}: scad. {data}, imp. € {imp}. — {studio}"),

            _ => (
                $"Scadenza {codiceTipo} – {nomeAzienda} – {data}",
                $"Gentile cliente,\n\nricordiamo la scadenza del {data} relativa a {nomeAzienda}.\n\nCordiali saluti,\n{studio}",
                $"📅 {codiceTipo} {nomeAzienda}: scad. {data}. — {studio}"),
        };

        return new MessaggioNotifica(oggetto, corpo.TrimStart(), breve);
    }
}
