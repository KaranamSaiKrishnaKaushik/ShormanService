# Theme Customization

The frontend now has a basic central theme layer for fonts, icons, colors, and backgrounds.

## Where to change things

Main theme variables live in `src/styles.scss`.

Google font and icon loading lives in `src/index.html`.

## 1. Change fonts quickly

Edit these variables in `src/styles.scss`:

```scss
--font-body: 'Manrope', ...;
--font-heading: 'Space Grotesk', ...;
--font-ui: 'DM Sans', ...;
--font-display: 'Outfit', ...;
```

Fonts currently loaded from Google:

- `Manrope`
- `Space Grotesk`
- `DM Sans`
- `Outfit`

Example swaps:

```scss
--font-body: 'DM Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
--font-heading: 'Outfit', 'Space Grotesk', 'Segoe UI', sans-serif;
```

If you want a completely different Google font:

1. add it to the Google Fonts link in `src/index.html`
2. point one of the font variables to that new family in `src/styles.scss`

## 2. Change icon style globally

The app now uses Google Material Symbols for shared shell icons.

Change this variable in `src/styles.scss`:

```scss
--icon-family: 'Material Symbols Rounded';
```

To switch style:

```scss
--icon-family: 'Material Symbols Outlined';
```

To change a specific icon, edit the icon text in the template.

Examples:

- `shopping_cart`
- `shopping_bag`
- `notifications`
- `menu`
- `local_mall`

## 3. Change colors and backgrounds

Edit these variables in `src/styles.scss`:

```scss
--color-primary: #1f6a35;
--color-primary-strong: #174d2b;
--color-primary-soft: #e9f6ed;
--color-accent: #ff8a1f;
--color-text: #18221c;
--color-text-muted: #5a6c60;
--color-surface: #ffffff;
--color-surface-alt: #f4f8f4;
--color-border: #d6e3d7;
--page-bg: #f7faf7;
--page-bg-accent: ...;
```

This already drives the global body, navbar, footer, and shared shell styling.

## 4. What is already theme-driven now

- global body font and heading font
- navbar colors and icons
- footer colors and icons
- shell spacing via `--navbar-height`

## 5. What is still not fully centralized yet

Some feature pages still use local hardcoded colors inside their own component SCSS files, especially larger screens like products and checkout.

That means the theme system is now in place, but a full app-wide theme rollout would still involve gradually replacing page-level hardcoded colors with the shared variables.