"use client";

import { useState } from "react";

const states = [
  ["AL", "Alabama"], ["AK", "Alaska"], ["AZ", "Arizona"], ["AR", "Arkansas"],
  ["CA", "California"], ["CO", "Colorado"], ["CT", "Connecticut"], ["DE", "Delaware"],
  ["DC", "District of Columbia"], ["FL", "Florida"], ["GA", "Georgia"], ["HI", "Hawaii"],
  ["ID", "Idaho"], ["IL", "Illinois"], ["IN", "Indiana"], ["IA", "Iowa"],
  ["KS", "Kansas"], ["KY", "Kentucky"], ["LA", "Louisiana"], ["ME", "Maine"],
  ["MD", "Maryland"], ["MA", "Massachusetts"], ["MI", "Michigan"], ["MN", "Minnesota"],
  ["MS", "Mississippi"], ["MO", "Missouri"], ["MT", "Montana"], ["NE", "Nebraska"],
  ["NV", "Nevada"], ["NH", "New Hampshire"], ["NJ", "New Jersey"], ["NM", "New Mexico"],
  ["NY", "New York"], ["NC", "North Carolina"], ["ND", "North Dakota"], ["OH", "Ohio"],
  ["OK", "Oklahoma"], ["OR", "Oregon"], ["PA", "Pennsylvania"], ["RI", "Rhode Island"],
  ["SC", "South Carolina"], ["SD", "South Dakota"], ["TN", "Tennessee"], ["TX", "Texas"],
  ["UT", "Utah"], ["VT", "Vermont"], ["VA", "Virginia"], ["WA", "Washington"],
  ["WV", "West Virginia"], ["WI", "Wisconsin"], ["WY", "Wyoming"]
] as const;

type StatePickerProps = {
  value: string;
  onChange: (value: string) => void;
};

export default function StatePicker({ value, onChange }: StatePickerProps) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const search = value ? "" : query.trim().toLowerCase();
  const matches = states.filter(([code, name]) =>
    name.toLowerCase().includes(search) || code.toLowerCase().includes(search)
  );

  function select(code: string, name: string) {
    setQuery(name);
    onChange(code);
    setOpen(false);
  }

  return <div className="state-picker" onBlur={(event) => {
    if (!(event.relatedTarget instanceof Node) || !event.currentTarget.contains(event.relatedTarget)) setOpen(false);
  }}>
    <label htmlFor="state-search">State</label>
    <input
      id="state-search"
      role="combobox"
      aria-autocomplete="list"
      aria-expanded={open}
      aria-controls="state-options"
      aria-activedescendant={open && matches.length ? `state-option-${activeIndex}` : undefined}
      autoComplete="off"
      placeholder="Search states"
      value={query}
      required
      onFocus={() => { setOpen(true); setActiveIndex(0); }}
      onChange={(event) => { setQuery(event.target.value); onChange(""); setOpen(true); setActiveIndex(0); }}
      onKeyDown={(event) => {
        if (event.key === "Escape") setOpen(false);
        if (event.key === "ArrowDown") { event.preventDefault(); setOpen(true); setActiveIndex((index) => Math.min(index + 1, matches.length - 1)); }
        if (event.key === "ArrowUp") { event.preventDefault(); setOpen(true); setActiveIndex((index) => Math.max(index - 1, 0)); }
        if (event.key === "Enter" && open && matches.length) {
          event.preventDefault();
          const [code, name] = matches[activeIndex];
          select(code, name);
        }
      }}
    />
    <input type="hidden" name="state" value={value} />
    {open && <div id="state-options" className="state-options" role="listbox" aria-label="States">
      {matches.length ? matches.map(([code, name], index) =>
        <button
          type="button"
          id={`state-option-${index}`}
          role="option"
          aria-selected={index === activeIndex}
          className={index === activeIndex ? "state-option active" : "state-option"}
          key={code}
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => select(code, name)}
        ><span>{name}</span><span className="state-code">{code}</span></button>
      ) : <div className="state-empty">No matching states</div>}
    </div>}
  </div>;
}
