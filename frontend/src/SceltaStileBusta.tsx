import {
  CATALOGO_STILI_BUSTA,
  GRUPPI_STILI_BUSTA,
  STILE_BUSTA_PREDEFINITO,
  dettaglioStileBusta,
  type StileBusta,
} from "./preferenze";

export function SceltaStileBusta({
  value,
  onChange,
  disabled = false,
  id,
  ariaLabel,
}: {
  value: StileBusta;
  onChange: (stile: StileBusta) => void;
  disabled?: boolean;
  id?: string;
  ariaLabel?: string;
}) {
  const dettaglio = dettaglioStileBusta(value);

  return (
    <>
      <select
        id={id}
        aria-label={ariaLabel}
        value={value}
        disabled={disabled}
        onChange={(evento) => onChange(evento.target.value as StileBusta)}
      >
        {GRUPPI_STILI_BUSTA.map((gruppo) => (
          <optgroup key={gruppo} label={gruppo}>
            {CATALOGO_STILI_BUSTA.filter((stile) => stile.gruppo === gruppo).map((stile) => (
              <option key={stile.id} value={stile.id}>
                {stile.nome}{stile.id === STILE_BUSTA_PREDEFINITO ? " (predefinito)" : ""}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
      <div
        role="note"
        style={{
          display: "flex",
          gap: 9,
          alignItems: "center",
          marginTop: 7,
          padding: "8px 10px",
          border: `1px solid ${dettaglio.accento}44`,
          borderLeft: `5px solid ${dettaglio.accento}`,
          borderRadius: 8,
          background: dettaglio.sfondo,
          color: "#172033",
        }}
      >
        <span
          aria-hidden="true"
          style={{ width: 22, height: 22, flex: "0 0 22px", borderRadius: 5, background: dettaglio.accento }}
        />
        <span style={{ minWidth: 0 }}>
          <strong style={{ display: "block", fontSize: 12 }}>{dettaglio.nome}</strong>
          <small style={{ display: "block", marginTop: 2 }}>{dettaglio.descrizione}</small>
        </span>
      </div>
    </>
  );
}
