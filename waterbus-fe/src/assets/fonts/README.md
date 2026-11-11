# Icon font

`material-symbols-outlined.woff2` is Material Symbols Outlined (Apache 2.0), cut down to the icons
the app uses so it weighs about 20 KB. It keeps the `FILL` axis, which `.material-symbols-outlined.filled` needs.

An icon that is not in the subset shows its name as plain text. When you use a new icon name,
rebuild the file: collect every icon name used in `src/` (sorted, comma separated) and run

```bash
curl -A "Mozilla/5.0 Chrome/120.0" "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0..1,0&icon_names=<names>"
```

then download the `.woff2` URL from the returned CSS over this file.
