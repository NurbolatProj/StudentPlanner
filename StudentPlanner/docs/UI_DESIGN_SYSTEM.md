# Student Planner UI design system

## Direction
The interface uses an institutional academic palette: deep slate blue for navigation and primary actions, muted teal for emphasis, cool neutral surfaces, and restrained semantic status colors. Typography uses Inter when installed and falls back to the system UI stack; no external font request is required.

## Tokens
The application shell defines `--primary`, `--primary-hover`, `--accent`, `--background`, `--surface`, `--surface-secondary`, `--text`, `--muted`, `--border`, and semantic `--success`, `--warning`, `--danger` values. `.app-shell.dark` switches these tokens and the native `color-scheme`, including browser date controls. Components inherit tokens to stay consistent across light and dark appearance.

## Components and interaction
Cards use a shared 14px radius and subtle shadow. Form controls use tokenized surfaces, clear labels, visible teal focus rings, invalid borders and readable disabled states. Primary buttons use slate blue; teal is reserved for secondary emphasis and focus. Validation stays adjacent to its field. The shell retains existing routes, modal interactions, and responsive sidebar behavior.

## Responsive and accessibility
The shell reduces page gutters at tablet and phone widths, statistics move to two columns and then one column on narrow phones, and controls retain keyboard focus visibility. Reduced-motion preferences disable decorative transitions. Native form controls use the active theme color scheme.
