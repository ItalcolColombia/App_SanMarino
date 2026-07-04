// tailwind.config.js - Paleta unificada Italfoods (logo: naranja + verde + crema)
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
        // ── NUEVA paleta de marca (derivada del logo Italcol: naranja + dorado + blanco) ──
        // Fuente única del refactor UX pro. ADITIVA: las clases legacy (ital-*, brand-red,
        // warm-gray-*) siguen para los módulos aún no migrados. Ver refactor_ux_pro_front_plan.md.
        primary: {
          DEFAULT: '#F5821F', 50: '#FFF4E6', 100: '#FFE3C2', 200: '#FCD09A',
          300: '#FBB040', 400: '#F8971F', 500: '#F5821F', 600: '#E86F12',
          700: '#C85A0E', 800: '#9A450B', 900: '#7A3708',
        },
        accent: {
          DEFAULT: '#FBB040', 50: '#FFF8EC', 100: '#FEEFCD', 300: '#FDD27A',
          500: '#FBB040', 600: '#E89A1E', 700: '#B9760F',
        },
        success: '#16A34A',
        warning: '#D97706',
        info:    '#2563EB',
        danger: { DEFAULT: '#DC2626', 50: '#FEECEC', 100: '#FCD2D2', 600: '#DC2626', 700: '#B91C1C', 900: '#7F1D1D' },
        ink:    { DEFAULT: '#1C1917', 2: '#57534E' },
        muted:  '#A8A29E',
        line:   '#E7E5E4',
        canvas: '#FAFAF9',
        surface: '#FFFFFF',

        // Naranja de marca (afinado al logo Italcol)
        'ital-orange': '#F5821F',
        'ital-orange-light': '#FBB040',
        'ital-orange-dark': '#C85A0E',
        'ital-orange-50': 'rgba(245, 130, 31, 0.08)',
        'ital-orange-100': 'rgba(245, 130, 31, 0.12)',
        // "ital-green" LEGACY remapeado → NARANJA (el verde salió de la marca; el logo no lo usa).
        // Las 242 clases *-ital-green pasan a naranja de una. Verde real = token `success`.
        'ital-green': '#F5821F',
        'ital-green-light': '#F8971F',
        'ital-green-dark': '#C85A0E',
        'ital-green-50': 'rgba(28, 25, 23, 0.05)',
        'ital-green-100': 'rgba(28, 25, 23, 0.10)',
        // Off-white cálido (fondo logo)
        'ital-cream': '#FAFAF9',
        'ital-cream-50': '#FFFFFF',
        // Texto secundario
        'ital-muted': '#6b7280',
        // Legacy / compatibilidad — brand-red afinado al danger del palette
        'brand-red': '#DC2626',
        'chicken-yellow': '#FBC02D',
        'warm-gray-100': '#F5F5F5',
        'warm-gray-300': '#E0E0E0',
        'warm-gray-500': '#9E9E9E',
        'warm-gray-700': '#616161',
      },
    },
  },
  plugins: [require('@tailwindcss/forms')],
};



