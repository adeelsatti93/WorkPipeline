import { useEffect, useRef, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { Layers, List, Play, ClipboardList, LogOut, Moon, Sun, Monitor, ChevronDown } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';

const navItems = [
  { to: '/workspaces', icon: Layers, label: 'Workspaces' },
  { to: '/todos', icon: List, label: 'Todo Board' },
  { to: '/review', icon: ClipboardList, label: 'Review Board' },
];

const THEMES = [
  { value: 'dark', label: 'Dark', icon: Moon },
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'system', label: 'System', icon: Monitor },
] as const;

type Theme = 'dark' | 'light' | 'system';

function resolveTheme(t: Theme): 'dark' | 'light' {
  if (t === 'system')
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  return t;
}

export default function Layout({ children }: { children: React.ReactNode }) {
  const { logout, username } = useAuthStore();
  const navigate = useNavigate();
  const [theme, setTheme] = useState<Theme>(() =>
    (localStorage.getItem('theme') as Theme) || 'dark'
  );
  const [themeOpen, setThemeOpen] = useState(false);
  const themeRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', resolveTheme(theme));
    localStorage.setItem('theme', theme);
  }, [theme]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (themeRef.current && !themeRef.current.contains(e.target as Node))
        setThemeOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const active = THEMES.find(t => t.value === theme)!;
  const ActiveIcon = active.icon;

  const handleLogout = () => { logout(); navigate('/login'); };

  return (
    <div className="layout-wrapper">
      <header className="layout-header">
        <div className="brand">
          <div className="logo-box"><Play size={14} /></div>
          <span className="brand-name">Work.Pipeline</span>
        </div>

        <nav className="main-nav">
          {navItems.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to}
              className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Icon size={16} />{label}
            </NavLink>
          ))}
        </nav>

        <div className="user-controls">
          <span className="username">{username}</span>

          {/* Theme dropdown */}
          <div className="theme-dropdown" ref={themeRef}>
            <button
              className="theme-trigger"
              onClick={() => setThemeOpen(v => !v)}
              title="Change theme"
            >
              <ActiveIcon size={14} />
              <span>{active.label}</span>
              <ChevronDown size={12} className={themeOpen ? 'rotated' : ''} />
            </button>

            {themeOpen && (
              <div className="theme-menu">
                {THEMES.map(({ value, label, icon: Icon }) => (
                  <button
                    key={value}
                    className={`theme-option ${theme === value ? 'active' : ''}`}
                    onClick={() => { setTheme(value); setThemeOpen(false); }}
                  >
                    <Icon size={14} />
                    {label}
                  </button>
                ))}
              </div>
            )}
          </div>

          <button onClick={handleLogout} className="logout-btn">
            <LogOut size={15} />Logout
          </button>
        </div>
      </header>

      <main className="layout-main">{children}</main>
    </div>
  );
}
