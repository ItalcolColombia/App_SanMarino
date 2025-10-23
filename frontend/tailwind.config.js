// tailwind.config.js
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
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



