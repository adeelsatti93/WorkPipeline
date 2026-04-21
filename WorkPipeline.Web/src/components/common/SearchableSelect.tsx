import { useState, useRef, useEffect } from 'react';
import { ChevronDown, Search, X } from 'lucide-react';

export interface SelectOption {
  value: string;
  label: string;
  sublabel?: string;
  badge?: string;
}

interface SearchableSelectProps {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  emptyMessage?: string;
}

export default function SearchableSelect({
  options,
  value,
  onChange,
  placeholder = 'Select…',
  disabled = false,
  emptyMessage = 'No results found',
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  const selected = options.find(o => o.value === value);

  const filtered = query.trim()
    ? options.filter(o =>
        o.label.toLowerCase().includes(query.toLowerCase()) ||
        o.sublabel?.toLowerCase().includes(query.toLowerCase()) ||
        o.value.toLowerCase().includes(query.toLowerCase())
      )
    : options;

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    if (open) {
      setQuery('');
      setTimeout(() => searchRef.current?.focus(), 50);
    }
  }, [open]);

  const handleSelect = (val: string) => {
    onChange(val);
    setOpen(false);
    setQuery('');
  };

  return (
    <div className={`searchable-select ${open ? 'open' : ''} ${disabled ? 'disabled' : ''}`} ref={containerRef}>
      <button
        type="button"
        className="ss-trigger"
        onClick={() => !disabled && setOpen(v => !v)}
        disabled={disabled}
      >
        <span className={`ss-value ${!selected ? 'placeholder' : ''}`}>
          {selected ? (
            <>
              <span>{selected.label}</span>
              {selected.sublabel && <span className="ss-sublabel">{selected.sublabel}</span>}
            </>
          ) : placeholder}
        </span>
        <ChevronDown size={14} className={`ss-chevron ${open ? 'rotated' : ''}`} />
      </button>

      {open && (
        <div className="ss-dropdown">
          <div className="ss-search">
            <Search size={13} />
            <input
              ref={searchRef}
              type="text"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder="Search…"
              onClick={e => e.stopPropagation()}
            />
            {query && (
              <button className="ss-clear" onClick={() => setQuery('')}>
                <X size={12} />
              </button>
            )}
          </div>

          <div className="ss-options">
            {filtered.length === 0 ? (
              <div className="ss-empty">{emptyMessage}</div>
            ) : (
              filtered.map(o => (
                <button
                  key={o.value}
                  type="button"
                  className={`ss-option ${o.value === value ? 'selected' : ''}`}
                  onClick={() => handleSelect(o.value)}
                >
                  <span className="ss-opt-label">{o.label}</span>
                  {o.sublabel && <span className="ss-opt-sub">{o.sublabel}</span>}
                  {o.badge && <span className="ss-opt-badge">{o.badge}</span>}
                </button>
              ))
            )}
          </div>

          {options.length > 0 && (
            <div className="ss-footer">
              {filtered.length} of {options.length}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
