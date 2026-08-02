import { Moon, Sun } from 'lucide-react'
import { useThemeStore } from './store'

export function ThemeToggle() {
  const theme = useThemeStore((s) => s.theme)
  const toggle = useThemeStore((s) => s.toggle)

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={
        theme === 'dark' ? 'Activer le thème clair' : 'Activer le thème sombre'
      }
      className="border-border bg-surface text-foreground hover:bg-accent hover:text-accent-foreground rounded-md border p-2 transition-colors"
    >
      {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
    </button>
  )
}
