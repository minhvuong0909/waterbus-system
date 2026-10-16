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
