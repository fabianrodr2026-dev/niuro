"use client";

import { useEffect, useRef, useState } from "react";

type Suggestion = { line1: string; label: string };

export default function AddressPicker() {
  const [query, setQuery] = useState("");
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const [selected, setSelected] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [lookupEnabled, setLookupEnabled] = useState<boolean | null>(null);
  const lookupDisabled = useRef(false);

  useEffect(() => {
    if (selected || lookupDisabled.current || query.trim().length < 3) {
      setSuggestions([]);
      return;
    }

    const controller = new AbortController();
    const timeout = setTimeout(async () => {
      try {
        const response = await fetch(`/api/address-suggestions?text=${encodeURIComponent(query.trim())}`, { signal: controller.signal });
        if (!response.ok) throw new Error("Address lookup unavailable");
        const data = await response.json();
        if (data.enabled === false) {
          lookupDisabled.current = true;
          setLookupEnabled(false);
          setSuggestions([]);
          setOpen(false);
          return;
        }
        setLookupEnabled(true);
        setSuggestions(data.suggestions);
        setUnavailable(false);
        setActiveIndex(0);
        setOpen(true);
      } catch {
        if (!controller.signal.aborted) {
          setSuggestions([]);
          setUnavailable(true);
          setOpen(true);
        }
      }
    }, 300);

    return () => { clearTimeout(timeout); controller.abort(); };
  }, [query, selected]);

  function select(suggestion: Suggestion) {
    setQuery(suggestion.line1);
    setSelected(true);
    setOpen(false);
  }

  return <div className="address-picker wide" onBlur={(event) => {
    if (!(event.relatedTarget instanceof Node) || !event.currentTarget.contains(event.relatedTarget)) setOpen(false);
  }}>
    <label htmlFor="address-search">Street address</label>
    <input
      id="address-search"
      name="address"
      role="combobox"
      aria-autocomplete="list"
      aria-expanded={open}
      aria-controls="address-options"
      aria-activedescendant={open && suggestions.length ? `address-option-${activeIndex}` : undefined}
      autoComplete="street-address"
      placeholder="Start typing a street address"
      value={query}
      required
      onFocus={() => { if (lookupEnabled !== false && query.length >= 3 && !selected) setOpen(true); }}
      onChange={(event) => { setQuery(event.target.value); setSelected(false); setUnavailable(false); setOpen(false); }}
      onKeyDown={(event) => {
        if (event.key === "Escape") setOpen(false);
        if (event.key === "ArrowDown" && suggestions.length) { event.preventDefault(); setOpen(true); setActiveIndex((index) => Math.min(index + 1, suggestions.length - 1)); }
        if (event.key === "ArrowUp" && suggestions.length) { event.preventDefault(); setOpen(true); setActiveIndex((index) => Math.max(index - 1, 0)); }
        if (event.key === "Enter" && open && suggestions.length) { event.preventDefault(); select(suggestions[activeIndex]); }
      }}
    />
    {open && (suggestions.length > 0 || unavailable) && <div id="address-options" className="state-options" role="listbox" aria-label="Address suggestions">
      {suggestions.map((suggestion, index) => <button
        type="button"
        id={`address-option-${index}`}
        role="option"
        aria-selected={index === activeIndex}
        className={index === activeIndex ? "state-option active" : "state-option"}
        key={`${suggestion.label}-${index}`}
        onMouseDown={(event) => event.preventDefault()}
        onClick={() => select(suggestion)}
      >{suggestion.label}</button>)}
      {unavailable && <div className="state-empty">Suggestions unavailable. You can enter the address manually.</div>}
    </div>}
    {lookupEnabled && <span className="address-attribution">Suggestions by <a href="https://www.geoapify.com/">Geoapify</a> · <a href="https://www.openstreetmap.org/copyright">© OpenStreetMap contributors</a></span>}
  </div>;
}
