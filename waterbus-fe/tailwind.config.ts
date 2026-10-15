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
        'surface-variant': '#e3e2e4',
        'secondary-fixed-dim': '#7fd4d8',
        'surface-bright': '#faf9fb',
        'on-secondary-container': '#007074',
        outline: '#73777d',
        'error-container': '#ffdad6',
        'on-tertiary': '#ffffff',
        'inverse-primary': '#b2c9e2',
        background: '#faf9fb',
        tertiary: '#190a00',
        'secondary-container': '#9bf1f5',
        'secondary-fixed': '#9bf1f5',
        'on-tertiary-fixed': '#2c1600',
        'on-background': '#1b1c1d',
        primary: '#000f1d',
        'primary-fixed-dim': '#b2c9e2',
        'on-primary': '#ffffff',
        'primary-container': '#0d2538',
        'deep-river': '#0D2538',
        'surface-container-highest': '#e3e2e4',
        'tertiary-fixed-dim': '#e8bf98',
        'on-surface': '#1b1c1d',
        'teal-flow': '#147A7E',
        'surface-container-low': '#f5f3f5',
        'on-secondary-fixed-variant': '#004f52',
        'on-primary-container': '#778da4',
        'surface-container-high': '#e9e8e9',
        'on-error': '#ffffff',
        'on-primary-fixed': '#041d30',
        'on-surface-variant': '#43474c',
        'signal-amber': '#E8B63E',
        'on-primary-fixed-variant': '#33495d',
        'on-secondary-fixed': '#002021',
        'inverse-surface': '#303032',
        'on-tertiary-container': '#a88460',
        'sky-aqua': '#4FC3D8',
        'surface-container-lowest': '#ffffff',
        'outline-variant': '#c3c7cd',
        'tertiary-fixed': '#ffdcbd',
        'on-secondary': '#ffffff',
        mist: '#F4F7F8',
        secondary: '#00696d',
        'surface-dim': '#dbd9db',
        surface: '#faf9fb',
        'surface-tint': '#4b6176',
        'on-error-container': '#93000a',
        'on-tertiary-fixed-variant': '#5d4123',
        'primary-fixed': '#cee5ff',
        'surface-container': '#efedef',
        error: '#ba1a1a',
        ink: '#18242D',
        'inverse-on-surface': '#f2f0f2',
        'tertiary-container': '#351e04',
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
      spacing: {
        'space-3xl': '64px',
        'space-sm': '8px',
        gutter: '24px',
        margin: '32px',
        'space-lg': '24px',
        'space-md': '16px',
        'space-2xl': '48px',
        'space-xl': '32px',
        'space-xs': '4px',
      },
      fontFamily: {
        'body-lg': ['Inter', 'sans-serif'],
        'body-md': ['Inter', 'sans-serif'],
        'body-sm': ['Inter', 'sans-serif'],
        'numeric-md': ['Plus Jakarta Sans', 'sans-serif'],
        'numeric-lg': ['Plus Jakarta Sans', 'sans-serif'],
        'headline-sm': ['Plus Jakarta Sans', 'sans-serif'],
        'headline-md': ['Plus Jakarta Sans', 'sans-serif'],
        'headline-lg': ['Plus Jakarta Sans', 'sans-serif'],
        'headline-xl': ['Plus Jakarta Sans', 'sans-serif'],
        'headline-xl-mobile': ['Plus Jakarta Sans', 'sans-serif'],
        script: ['Caveat', 'cursive'],
      },
      fontSize: {
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
        'numeric-md': [
          '18px',
          {
            lineHeight: '24px',
            fontWeight: '600',
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
