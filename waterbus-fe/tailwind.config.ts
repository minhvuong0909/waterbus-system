import type { Config } from 'tailwindcss'
import forms from '@tailwindcss/forms'
import containerQueries from '@tailwindcss/container-queries'

// Design tokens come from docs/stitch/.../smart_waterbus/DESIGN.md.
// This is the single source of truth for colors, type scale, radius and spacing.
const config: Config = {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        'sand-light': '#E9F0EC',
        'coral-glow': '#F08A6B',
        'on-secondary-container': '#007074',
        outline: '#73777d',
        'secondary-container': '#9bf1f5',
        'secondary-fixed': '#9bf1f5',
        'on-primary': '#ffffff',
        'primary-container': '#0d2538',
        'deep-river': '#0D2538',
        'on-surface': '#1b1c1d',
        'teal-flow': '#147A7E',
        'surface-container-low': '#f5f3f5',
        'surface-container-high': '#e9e8e9',
        'on-surface-variant': '#43474c',
        'signal-amber': '#E8B63E',
        'sky-aqua': '#4FC3D8',
        'surface-container-lowest': '#ffffff',
        'outline-variant': '#c3c7cd',
        mist: '#F4F7F8',
        secondary: '#00696d',
        surface: '#faf9fb',
        'surface-container': '#efedef',
        ink: '#18242D',
      },
      borderRadius: {
        DEFAULT: '0.25rem',
        lg: '0.5rem',
        xl: '0.75rem',
        '2xl': '1rem',
        '3xl': '1.5rem',
        full: '9999px',
        card: '16px',
        panel: '24px',
      },
      boxShadow: {
        /** Resting cards and panels. */
        card: '0 2px 16px rgba(13, 37, 56, 0.05)',
        /** Raised surfaces: sticky summaries, maps, popovers. */
        raised: '0 4px 24px rgba(13, 37, 56, 0.08)',
        /** Floating layers above the page. */
        overlay: '0 6px 24px rgba(13, 37, 56, 0.15)',
        /** Primary call to action. */
        glow: '0 2px 12px rgba(20, 122, 126, 0.25)',
      },
      spacing: {
        'space-3xl': '64px',
        'space-sm': '8px',
        margin: '32px',
        'space-lg': '24px',
        'space-md': '16px',
        'space-2xl': '48px',
        'space-xl': '32px',
        'space-xs': '4px',
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        /** Headings, prices and other display text. */
        display: ['Plus Jakarta Sans', 'sans-serif'],
      },
      fontSize: {
        /** Micro labels inside seat-map cells. */
        '3xs': ['9px', { lineHeight: '12px' }],
        /** Captions, badges and table headers. */
        '2xs': ['11px', { lineHeight: '16px' }],
        'body-sm': [
          '12px',
          {
            lineHeight: '16px',
            fontWeight: '400',
          },
        ],
        'body-md': [
          '14px',
          {
            lineHeight: '20px',
            fontWeight: '400',
          },
        ],
        'body-lg': [
          '16px',
          {
            lineHeight: '24px',
            fontWeight: '400',
          },
        ],
        'numeric-lg': [
          '32px',
          {
            lineHeight: '38px',
            fontWeight: '700',
          },
        ],
        'headline-sm': [
          '20px',
          {
            lineHeight: '28px',
            fontWeight: '600',
          },
        ],
        'headline-md': [
          '26px',
          {
            lineHeight: '34px',
            fontWeight: '600',
          },
        ],
        'headline-lg': [
          '38px',
          {
            lineHeight: '46px',
            letterSpacing: '-0.01em',
            fontWeight: '600',
          },
        ],
        'headline-xl': [
          '56px',
          {
            lineHeight: '64px',
            letterSpacing: '-0.02em',
            fontWeight: '700',
          },
        ],
        'headline-xl-mobile': [
          '36px',
          {
            lineHeight: '44px',
            letterSpacing: '-0.01em',
            fontWeight: '700',
          },
        ],
      },
    },
  },
  plugins: [forms, containerQueries],
}

export default config
