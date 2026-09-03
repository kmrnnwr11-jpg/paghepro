using System.Collections.Concurrent;

namespace Paghe.Persistence.Ai;

/// <summary>
/// Rollout A3 fail-closed. Due strade distinte, entrambe per coppia esatta tenant/subject:
/// le coppie di collaudo dalla configurazione (`Ai:CoppieShadowA3`) e — dal 3 settembre
/// 2026, per decisione del fondatore — il consenso pieno ad Aurora registrato sul database
/// (tabella ConsensiAurora), caricato all'avvio e aggiornato dagli endpoint del consenso.
/// Il consenso vale come Confirmed: le proposte restano card con conferma esplicita e undo.
/// `Ai:A3ConConsensoPieno=false` è il kill-switch che spegne la strada del consenso.
/// Nessun indirizzo email concede autonomia.
/// </summary>
public sealed class AiRolloutPolicy
{
    private readonly HashSet<(Guid TenantId, Guid UtenteId)> _coppie;
    private readonly ConcurrentDictionary<(Guid TenantId, Guid UtenteId), byte> _consensi = new();

    public string Gate { get; }
    public string ModalitaA3 { get; }

    /// <summary>Kill-switch della strada del consenso (config `Ai:A3ConConsensoPieno`).</summary>
    public bool A3ConConsensoPieno { get; }

    public static AiRolloutPolicy SoloA2 { get; } = new("A2", "Off", []);

    public AiRolloutPolicy(
        string? gate, string? modalitaA3, IEnumerable<string>? coppie,
        bool a3ConConsensoPieno = true)
    {
        Gate = string.IsNullOrWhiteSpace(gate) ? "A2" : gate.Trim();
        ModalitaA3 = string.IsNullOrWhiteSpace(modalitaA3) ? "Off" : modalitaA3.Trim();
        A3ConConsensoPieno = a3ConConsensoPieno;
        if (Gate is not ("A2" or "A3"))
            throw new InvalidOperationException("Ai:GateAurora deve essere A2 oppure A3.");
        if (ModalitaA3 is not ("Off" or "DryRun" or "Confirmed"))
            throw new InvalidOperationException(
                "Ai:ModalitaA3 deve essere Off, DryRun oppure Confirmed.");
        if (Gate == "A2" && ModalitaA3 != "Off")
            throw new InvalidOperationException(
                "Ai:ModalitaA3 deve restare Off quando Ai:GateAurora è A2.");

        _coppie = [];
        foreach (var voce in coppie ?? [])
        {
            var parti = (voce ?? "").Split(':', StringSplitOptions.TrimEntries);
            if (parti.Length != 2
                || !Guid.TryParse(parti[0], out var tenantId)
                || !Guid.TryParse(parti[1], out var utenteId)
                || tenantId == Guid.Empty || utenteId == Guid.Empty)
                throw new InvalidOperationException(
                    "Ogni Ai:CoppieShadowA3 deve essere tenant-guid:utente-guid.");
            _coppie.Add((tenantId, utenteId));
        }
        if (Gate == "A3" && ModalitaA3 != "Off" && _coppie.Count == 0)
            throw new InvalidOperationException(
                "Il rollout A3 richiede almeno una coppia tenant/utente esplicita.");
    }

    /// <summary>Consenso pieno concesso: da chiamare all'avvio (bootstrap) e alla concessione.</summary>
    public void RegistraConsenso(Guid tenantId, Guid utenteId)
    {
        if (tenantId != Guid.Empty && utenteId != Guid.Empty)
            _consensi[(tenantId, utenteId)] = 0;
    }

    /// <summary>Consenso revocato: la coppia perde subito la strada A3 del consenso.</summary>
    public void RimuoviConsenso(Guid tenantId, Guid utenteId) =>
        _consensi.TryRemove((tenantId, utenteId), out _);

    private bool ConsensoAttivo(Guid tenantId, Guid utenteId) =>
        A3ConConsensoPieno && _consensi.ContainsKey((tenantId, utenteId));

    public bool ProposteAbilitate(Guid tenantId, Guid utenteId) =>
        (Gate == "A3" && ModalitaA3 is "DryRun" or "Confirmed"
            && _coppie.Contains((tenantId, utenteId)))
        || ConsensoAttivo(tenantId, utenteId);

    public bool EffettiAbilitati(Guid tenantId, Guid utenteId) =>
        (Gate == "A3" && ModalitaA3 == "Confirmed"
            && _coppie.Contains((tenantId, utenteId)))
        || ConsensoAttivo(tenantId, utenteId);
}
