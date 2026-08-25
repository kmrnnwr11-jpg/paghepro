import { useEffect, useState } from "react";
import { caricaComuni, cercaComuni, type Comune } from "./dati";

interface Props {
  valore: string;
  onSelect: (c: Comune) => void;
  onInputChange?: (valore: string) => void;
  placeholder?: string;
  required?: boolean;
}

export function ComuneAutocomplete({ valore, onSelect, onInputChange, placeholder, required }: Props) {
  const [tutti, setTutti] = useState<Comune[]>([]);
  const [testo, setTesto] = useState(valore);
  const [suggerimenti, setSuggerimenti] = useState<Comune[]>([]);
  const [aperto, setAperto] = useState(false);

  useEffect(() => { caricaComuni().then(setTutti).catch(() => { /* offline */ }); }, []);
  useEffect(() => { setTesto(valore); }, [valore]);

  function aggiorna(v: string) {
    setTesto(v);
    onInputChange?.(v);
    setSuggerimenti(cercaComuni(tutti, v));
    setAperto(true);
  }

  function scegli(c: Comune) {
    setTesto(c.nome);
    setSuggerimenti([]);
    setAperto(false);
    onSelect(c);
  }

  return (
    <div className="autocomplete">
      <input
        value={testo}
        placeholder={placeholder ?? "Scrivi il comune…"}
        onChange={(e) => aggiorna(e.target.value)}
        onFocus={() => setSuggerimenti(cercaComuni(tutti, testo))}
        onBlur={() => setTimeout(() => setAperto(false), 150)}
        autoComplete="off"
        required={required}
      />
      {aperto && suggerimenti.length > 0 && (
        <ul className="suggerimenti">
          {suggerimenti.map((c) => (
            <li key={c.cat} onMouseDown={() => scegli(c)}>
              {c.nome} <span className="muto">({c.prov}) · {c.reg}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
