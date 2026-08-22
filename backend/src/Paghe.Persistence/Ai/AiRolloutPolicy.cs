namespace Paghe.Persistence.Ai;

/// <summary>
/// Rollout A3 fail-closed basato sulla coppia esatta tenant/subject. È condiviso da
/// emissione grant, registry e conferma: nessun indirizzo email concede autonomia.
/// </summary>
public sealed class AiRolloutPolicy
{
    private readonly HashSet<(Guid TenantId, Guid UtenteId)> _coppie;

    public string Gate { get; }
    public string ModalitaA3 { get; }

    public static AiRolloutPolicy SoloA2 { get; } = new("A2", "Off", []);

    public AiRolloutPolicy(string? gate, string? modalitaA3, IEnumerable<string>? coppie)
    {
        Gate = string.IsNullOrWhiteSpace(gate) ? "A2" : gate.Trim();
        ModalitaA3 = string.IsNullOrWhiteSpace(modalitaA3) ? "Off" : modalitaA3.Trim();
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

    public bool ProposteAbilitate(Guid tenantId, Guid utenteId) =>
        Gate == "A3" && ModalitaA3 is "DryRun" or "Confirmed"
        && _coppie.Contains((tenantId, utenteId));

    public bool EffettiAbilitati(Guid tenantId, Guid utenteId) =>
        Gate == "A3" && ModalitaA3 == "Confirmed"
        && _coppie.Contains((tenantId, utenteId));
}
