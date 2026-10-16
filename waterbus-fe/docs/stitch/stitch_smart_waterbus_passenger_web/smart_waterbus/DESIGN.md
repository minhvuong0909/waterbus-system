---
name: Smart Waterbus
colors:
  surface: '#faf9fb'
  surface-dim: '#dbd9db'
  surface-bright: '#faf9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f5f3f5'
  surface-container: '#efedef'
  surface-container-high: '#e9e8e9'
  surface-container-highest: '#e3e2e4'
  on-surface: '#1b1c1d'
  on-surface-variant: '#43474c'
  inverse-surface: '#303032'
  inverse-on-surface: '#f2f0f2'
  outline: '#73777d'
  outline-variant: '#c3c7cd'
  surface-tint: '#4b6176'
  primary: '#000f1d'
  on-primary: '#ffffff'
  primary-container: '#0d2538'
  on-primary-container: '#778da4'
  inverse-primary: '#b2c9e2'
  secondary: '#00696d'
  on-secondary: '#ffffff'
  secondary-container: '#9bf1f5'
  on-secondary-container: '#007074'
  tertiary: '#190a00'
  on-tertiary: '#ffffff'
  tertiary-container: '#351e04'
  on-tertiary-container: '#a88460'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#cee5ff'
  primary-fixed-dim: '#b2c9e2'
  on-primary-fixed: '#041d30'
  on-primary-fixed-variant: '#33495d'
  secondary-fixed: '#9bf1f5'
  secondary-fixed-dim: '#7fd4d8'
  on-secondary-fixed: '#002021'
  on-secondary-fixed-variant: '#004f52'
  tertiary-fixed: '#ffdcbd'
  tertiary-fixed-dim: '#e8bf98'
  on-tertiary-fixed: '#2c1600'
  on-tertiary-fixed-variant: '#5d4123'
  background: '#faf9fb'
  on-background: '#1b1c1d'
  surface-variant: '#e3e2e4'
  deep-river: '#0D2538'
  teal-flow: '#147A7E'
  sky-aqua: '#4FC3D8'
  mist: '#F4F7F8'
  sand-light: '#E9F0EC'
  ink: '#18242D'
  coral-glow: '#F08A6B'
  signal-amber: '#E8B63E'
typography:
  headline-xl:
    fontFamily: Plus Jakarta Sans
    fontSize: 56px
    fontWeight: '700'
    lineHeight: 64px
    letterSpacing: -0.02em
  headline-xl-mobile:
    fontFamily: Plus Jakarta Sans
    fontSize: 36px
    fontWeight: '700'
    lineHeight: 44px
    letterSpacing: -0.01em
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 38px
    fontWeight: '600'
    lineHeight: 46px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 26px
    fontWeight: '600'
    lineHeight: 34px
  headline-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  numeric-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 38px
  numeric-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 24px
  margin: 32px
  space-xs: 4px
  space-sm: 8px
  space-md: 16px
  space-lg: 24px
  space-xl: 32px
  space-2xl: 48px
  space-3xl: 64px
---

# Smart Waterbus — DESIGN.md

## 1. Product Identity

**Product:** Smart Waterbus  
**Design concept:** Urban River Flow

Smart Waterbus is a modern urban river transportation + smart tourism platform.

The visual identity must feel:
- modern
- calm
- refreshing
- fluid
- trustworthy
- premium but practical
- technology-driven, but not sci-fi
- tourism-aware, but not like a generic travel website

The product must have its own identity. Do not imitate any existing waterbus brand.

---

## 2. Design Principles

1. **Transport first, tourism second**
   - Booking, tickets, tracking and operations must remain clear and practical.
   - Tourism content may be more editorial and immersive.

2. **Movement without visual noise**
   - Express river motion through route lines, directional layout, vessel orientation, subtle depth and layered composition.
   - Avoid decorative wave shapes everywhere.

3. **3D is a signature accent, not the UI language**
   - Use 3D / 3D-looking vessels only in high-impact brand moments.
   - Do not use 3D in forms, checkout, payment, data tables or dense admin screens.

4. **Maps are a signature surface**
   - River corridor and active route should visually dominate.
   - Pier nodes and vessel position must be immediately understandable.
   - Tourism POIs should be secondary.

5. **Buildable, not concept-art-only**
   - Every screen should look realistically implementable in a production frontend.

---

## 3. Color System

### Core colors

- **Deep River** — `#0D2538`
- **Teal Flow** — `#147A7E`
- **Sky Aqua** — `#4FC3D8`
- **Mist** — `#F4F7F8`
- **Sand Light** — `#E9F0EC`
- **Ink** — `#18242D`

### Accent colors

- **Coral Glow** — `#F08A6B`
- **Signal Amber** — `#E8B63E`

### Color usage rules

- Passenger screens: Mist + Deep River + Teal Flow + Sky Aqua
- Tourism surfaces: may add Coral Glow and warmer imagery
- Admin/operations: stronger Deep River + Teal + Aqua
- Avoid generic bright-blue travel-app styling
- Avoid excessive gradients
- Never use color as the only status indicator

---

## 4. Typography

Use a clean modern sans-serif.

Preferred direction:
- **Plus Jakarta Sans**, **Manrope**, or similar for headings
- **Inter**, **Plus Jakarta Sans**, or system sans for body

### Hierarchy

- H1: 52–60px desktop, bold
- H2: 36–40px, semibold
- H3: 24–28px, semibold
- Body: 16px
- Small body: 14px
- Caption: 12px

Use:
- confident, compact headings
- readable body text
- strong numeric hierarchy for trip times, ticket codes, seat numbers and status

Avoid:
- decorative display fonts
- handwritten fonts as primary typography
- excessive all-caps

---

## 5. Spacing & Layout

Use a consistent spacing scale:

`4 / 8 / 12 / 16 / 24 / 32 / 48 / 64`

### Passenger Web
- Desktop-first
- Target width around 1440px
- Generous whitespace
- Clear section rhythm
- Prefer 12-column grid behavior

### Admin / Dispatcher
- Higher information density
- Desktop-first
- Sidebar navigation is preferred
- Preserve the same brand system but reduce decorative treatments

### Staff Mobile
- Utility-first
- Large touch targets
- Clear success/error states
- Scanner should be the main action

---

## 6. Radius, Borders & Elevation

- Standard card radius: **16px**
- Large feature panel / hero panel: **24px**
- Input / button radius: **12–14px**
- Small chips / tags: rounded, but not overly pill-shaped

Use:
- subtle 1px borders
- soft low-elevation shadows
- layered depth only where useful

Avoid:
- floating every card
- excessive glassmorphism
- strong drop shadows
- ultra-rounded “toy-like” UI

---

## 7. Buttons

### Primary
- Teal Flow background
- White text
- Medium-to-large height
- Strong contrast

### Secondary
- Light / transparent surface
- Teal or Deep River border/text

### Tertiary / Text
- Minimal, for lower-priority actions

Buttons should feel modern and practical, not playful.

---

## 8. Cards

Use three card families:

### Functional cards
For:
- trips
- tickets
- checkout
- payment
- forms

Style:
- clean
- simple
- information-first

### Journey cards
For:
- current trip
- live tracking
- weather
- next stop
- realtime status

Style:
- more visual emphasis
- map or route cues allowed

### Editorial cards
For:
- tourism
- destinations
- river experiences
- city discovery

Style:
- larger imagery
- more whitespace
- stronger storytelling

---

## 9. Maps

Maps are one of the main visual signatures of Smart Waterbus.

Rules:
- simplify unnecessary map details
- highlight the river corridor
- emphasize the active route
- use clear pier nodes
- use a distinctive vessel marker
- keep tourism POIs visually secondary
- make status readable without relying only on color

Passenger maps should feel calm and easy to understand.

Dispatcher maps may show:
- multiple vessels
- alerts
- route health
- weather / river conditions
- operational statuses

---

## 10. 3D / Vessel Treatment

Use a premium Smart Waterbus vessel as a signature visual asset.

Allowed:
- Landing hero
- Boat / trip detail
- Selected brand moments
- Onboarding / empty state illustration

Avoid:
- checkout
- payment
- refund
- dense forms
- admin tables
- operational control screens

For Stitch prototypes:
- use a polished static 3D-looking vessel render or placeholder

For final frontend:
- interactive 3D may be added later
- possible interactions: slight floating, mouse parallax, slow rotation, drag-to-rotate, subtle lighting / reflection

---

## 11. Motion Language

The static prototype should visually imply motion.

Future production motion may include:
- vessel movement along routes
- route progress animation
- subtle hero boat motion
- gentle section reveals
- bottom-sheet transitions
- low-amplitude parallax

Motion should feel:
- smooth
- calm
- directional
- purposeful

Avoid:
- bouncing
- excessive parallax
- flashy transitions
- motion that interrupts task completion

---

## 12. Tourism Visual Language

Tourism content should feel more editorial than booking flows.

Use:
- scenic river-city photography
- large image crops
- spacious layouts
- destination storytelling
- location / audio-guide cues

Avoid:
- travel brochure clichés
- excessive landmarks in every screen
- overdecorated destination cards

---

## 13. Icon Style

Use:
- modern outline icons
- consistent stroke weight
- slightly rounded geometry
- high clarity at small sizes

Custom branded icons may be used for:
- vessel
- pier
- route
- QR boarding
- audio guide
- incident / trip change

Avoid nautical clichés such as:
- anchors
- ship wheels
- decorative wave icons everywhere

---

## 14. Navigation

### Passenger Web
Preferred top navigation:
- Home
- Routes / Plan Journey
- Explore
- My Tickets
- optional AI Assistant entry
- Language
- Account

### Admin / Dispatcher
Preferred sidebar navigation:
- Overview
- Live Operations
- Trips
- Routes
- Bookings / Tickets
- Incidents
- Analytics
- Users
- Settings

### Staff Mobile
Keep navigation minimal.
Primary flow:
Login → Scan → Verification → Next Scan / Sync

---

## 15. Passenger Home Page

The Home / Landing page is primarily a product and brand introduction.

Do **not** place a full booking form in the hero.

Recommended structure:
1. Hero
2. More than transportation
3. Explore the river
4. Discover the city from the river
5. How it works
6. Smart journey features
7. Final CTA

Hero should include:
- strong brand headline
- concise supporting text
- CTA: **Plan your journey**
- secondary CTA: **Explore the river**
- premium Smart Waterbus vessel visual
- subtle route / river movement cues

---

## 16. Prototype Scope

### Passenger
Prototype as **Desktop Web first**.

Responsive Passenger Web is a later frontend implementation requirement and does not require separate Stitch mobile screens now.

### Mobile
Only the **Staff Scanner flow** is designed as dedicated mobile UI.

### Admin / Dispatcher
Desktop Web.

---

## 17. Do Not Do

Do not:
- imitate an existing waterbus brand
- make every screen blue
- use wave shapes everywhere
- overuse glassmorphism
- overuse gradients
- turn all interfaces into cards
- use 3D everywhere
- make operational screens decorative
- create generic SaaS dashboard styling
- create generic airline / bus booking visuals
- sacrifice readability for aesthetics

---

## 18. Consistency Rule

All generated screens must feel like one product family.

Keep consistent:
- color behavior
- typography
- spacing
- radii
- button language
- icon style
- map language
- status semantics
- vessel visual treatment

Passenger, Admin, and Staff may differ in information density and layout, but should still clearly belong to **Smart Waterbus**.
