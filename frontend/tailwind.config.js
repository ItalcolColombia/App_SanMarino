// tailwind.config.js - Paleta unificada Italfoods (logo: naranja + verde + crema)
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
        // Italfoods logo: naranja (ital, cuadros) — texto y acentos
        'ital-orange': '#e85c25',
        'ital-orange-light': '#f0a078',
        'ital-orange-dark': '#d14a18',
        'ital-orange-50': 'rgba(232, 92, 37, 0.08)',
        'ital-orange-100': 'rgba(232, 92, 37, 0.12)',
        // Italfoods logo: verde (foods, borde) — tablas, fondos, botones primarios
        'ital-green': '#2d7a3e',
        'ital-green-light': '#3d9b52',
        'ital-green-dark': '#1e5c2a',
        'ital-green-50': 'rgba(45, 122, 62, 0.08)',
        'ital-green-100': 'rgba(45, 122, 62, 0.12)',
        // Crema / off-white (fondo logo)
        'ital-cream': '#faf8f5',
        'ital-cream-50': '#fefdfb',
        // Texto secundario
        'ital-muted': '#6b7280',
        // Legacy / compatibilidad
        'brand-red': '#D32F2F',
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



