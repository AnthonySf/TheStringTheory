# Chart Editor Visual Library

The chart editor follows ToneLab's control language: dark surfaces, transparent controls, white outlines, restrained accent fills, large readable type, and small hover motion. Controls should look like production UI, not debug panels.

## Foundations

- Backgrounds are near-black panels with a subtle blue-gray tint.
- Surfaces use a 1 px border with a lighter top edge and darker lower edge.
- Buttons are transparent by default. Filled buttons are reserved for the primary commit action, such as Export, Apply, or Save.
- Rounded corners should stay moderate: 10-12 px for controls, 14-16 px for popups and context menus.
- Text must be large enough to read quickly: 14-16 px for compact controls, 18-24 px for editor-scale controls, 24-40 px for popup titles.
- Hover should feel lightweight: opacity to 1.0, scale to 1.02, and a subtle translucent fill.

## Buttons

### Secondary

Use for navigation, utility actions, neutral sidebar actions, and most context actions.

- Transparent background.
- White or cool-gray text.
- 1 px white/gray outline with slightly brighter top edge.
- Hover: white fill at low opacity, brighter text, scale 1.02.

### Primary

Use for final/positive actions only.

- Purple fill or strong purple outline.
- White bold text.
- Hover: slightly brighter purple fill, scale 1.02.

### Danger

Use for destructive actions.

- Transparent background.
- Red text and red outline.
- Hover: low-opacity red fill and white/red text.

### Icon Buttons

- Square or compact rounded outline, transparent background.
- Centered icon/text glyph.
- Same hover treatment as secondary buttons.
- Avoid decorative pills or ovals unless the control is genuinely a switch.

## Toggles

Toggles in the editor should read as compact outlined state buttons, not oversized mobile switches.

- Enabled: dark green-tinted fill, green outline, light green text.
- Disabled: transparent or near-transparent background, gray outline, gray text.
- Hover: subtle fill and 1.02 scale.

## Text Fields

- Dark input surface with a 1 px outline.
- White input text and muted labels.
- 10-12 px radius.
- Avoid white default Unity input backgrounds.

## Dropdowns

- Transparent surface with a single underline or subtle outline.
- White text with muted arrow.
- Hover/focus may brighten the underline.

## Sliders

- Slim rounded track, muted dark base.
- Purple accent fill.
- Light circular handle with a subtle outline.
- The label/value pair should be close to the slider.

## Context Menus

- Dark floating card, 14-16 px radius, 1 px border.
- Rows are transparent until hover.
- Row height should be comfortable, never tiny.
- Submenus use a small text hint such as `More` instead of decorative arrows or bars.
- Destructive rows use red text and red hover tint.

## Popups

- Centered dark card, 16 px radius, 1 px border.
- Clear title, short subtitle when useful.
- Large controls with consistent vertical spacing.
- Footer actions are right-aligned: secondary Cancel, filled primary Apply/Save.

## Sidebar Actions

- Full-width outlined buttons.
- Label on the left, short action state on the right.
- No filled debug rectangles.
- Hover uses the same ToneLab outline-button motion.
