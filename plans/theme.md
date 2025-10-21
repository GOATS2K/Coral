# Coral Theme Customization System - Implementation Plan

## Overview

This plan covers the implementation of a user-customizable theme system for Coral, allowing users to:
- Set custom background colors
- Set custom accent colors
- Toggle between light/dark/system modes
- Persist theme preferences across app restarts

## Current State Analysis

Coral currently uses:
- **NativeWind** with Tailwind CSS for styling
- **CSS Custom Properties** (`--background`, `--primary`, etc.) defined in `global.css`
- **Jotai atoms** for theme state management (`themeAtom`, `themePreferenceAtom`)
- **AsyncStorage/localStorage** for persistence
- **Light/Dark/System** theme modes

The theme is applied via CSS variables that NativeWind references (e.g., `hsl(var(--background))`).

---

## Architecture Overview

### 1. Theme Data Structure

```typescript
// lib/state.ts or lib/theme-state.ts

export interface CustomThemeColors {
  // User-customizable colors
  background?: string;      // HSL format: "220 13% 9%"
  accent?: string;          // HSL format: "142 71% 45%"
}

export interface ThemeConfig {
  mode: ThemePreference;     // 'light' | 'dark' | 'system'
  customColors: CustomThemeColors;
  useCustomColors: boolean;  // Toggle to enable/disable custom theme
}
```

### 2. Storage Mechanism

- **Native (iOS/Android):** `AsyncStorage`
- **Web:** `localStorage`
- **Storage key:** `'theme-config'`
- **Format:** JSON string containing `ThemeConfig`

**Migration strategy:** Keep existing `theme-preference` key for backward compatibility initially, then migrate to unified `theme-config`.

### 3. Jotai Atom Architecture

**New atoms to add in `lib/state.ts`:**

```typescript
// Custom theme configuration atom
export const themeConfigAtom = atom<ThemeConfig>(
  getInitialThemeConfig(),
  (get, set, newValue: Partial<ThemeConfig>) => {
    const current = get(themeConfigAtom);
    const updated = { ...current, ...newValue };
    set(themeConfigAtom, updated);

    // Persist to storage
    AsyncStorage.setItem('theme-config', JSON.stringify(updated)).catch(e =>
      console.error('[themeConfigAtom] Save error:', e)
    );
  }
);

// Computed atom for resolved theme colors (merges defaults + custom)
export const resolvedThemeColorsAtom = atom<ThemeColors>((get) => {
  const config = get(themeConfigAtom);
  const baseTheme = get(themeAtom); // 'light' or 'dark'

  if (!config.useCustomColors) {
    return getDefaultThemeColors(baseTheme);
  }

  return {
    ...getDefaultThemeColors(baseTheme),
    ...config.customColors
  };
});
```

### 4. Dynamic CSS Variable Injection

Create a new hook to inject custom CSS variables:

```typescript
// lib/hooks/use-theme-injection.ts

export function useThemeInjection() {
  const themeColors = useAtomValue(resolvedThemeColorsAtom);

  useEffect(() => {
    if (Platform.OS === 'web') {
      // Inject CSS variables directly into document root
      const root = document.documentElement;
      Object.entries(themeColors).forEach(([key, value]) => {
        if (value) {
          root.style.setProperty(`--${key}`, value);
        }
      });
    }
    // For native: Use React Context or pass down via props
  }, [themeColors]);
}
```

Call this hook in `app/_layout.tsx` inside `AppContent`.

### 5. Color Picker Component

**Recommended library:** `reanimated-color-picker`
- ✅ Supports iOS, Android, Web, and Expo
- ✅ Highly customizable
- ✅ Smooth animations with Reanimated

**Installation:**
```bash
bun add reanimated-color-picker
```

**Wrapper component:**
```typescript
// components/settings/color-picker.tsx

import ColorPicker from 'reanimated-color-picker';

interface ColorPickerDialogProps {
  initialColor: string;
  onColorSelect: (color: string) => void;
  label: string;
}

export function ColorPickerDialog({ initialColor, onColorSelect, label }: ColorPickerDialogProps) {
  // Convert HSL to hex for picker
  // Display color preview
  // Show picker in bottom sheet or modal
}
```

---

## Theme Customization Page Design

**Location:** `app/settings/theme.tsx`

**UI Structure:**

```typescript
- Use Custom Theme: [Toggle]

- Background Color
  [Color preview box] [Edit button]

- Accent Color
  [Color preview box] [Edit button]

- Preview
  [Preview section showing buttons/cards with current theme]

- Reset to Defaults [Button]
```

**User Flow:**

1. **Enable Custom Theme:**
   - User toggles "Use Custom Theme" on
   - Color pickers become enabled
   - App immediately applies any saved custom colors

2. **Pick Background Color:**
   - User taps "Edit" next to background color
   - Color picker modal/bottom sheet appears
   - User selects color
   - Preview updates in real-time
   - User confirms selection
   - Color saved to atom (auto-persisted)
   - CSS variables updated immediately

3. **Pick Accent Color:**
   - Same flow as background color
   - Primary, secondary, and accent colors updated

4. **Reset to Defaults:**
   - User taps "Reset to Defaults"
   - Confirmation dialog appears
   - On confirm: `useCustomColors` set to false, custom colors cleared
   - App reverts to default theme

---

## Implementation Steps

### Phase 1: Data Layer
1. Define `ThemeConfig` and `CustomThemeColors` types in `lib/state.ts`
2. Create `themeConfigAtom` with persistence
3. Add helper functions for HSL ↔ Hex conversion (using `colord` library)
4. Create `resolvedThemeColorsAtom` computed atom
5. Write migration function for existing `theme-preference` storage

### Phase 2: Dynamic Theming
6. Create `use-theme-injection.ts` hook for CSS variable injection
7. Integrate hook into `app/_layout.tsx`
8. Test that custom colors properly override default theme
9. Ensure native platform compatibility (may need React Context for native)

### Phase 3: UI Components
10. Install `reanimated-color-picker` and `colord`
11. Create `ColorPickerDialog` component
12. Create color preview component
13. Build theme preview component showing current colors in action

### Phase 4: Theme Settings Page
14. Create `app/settings/theme.tsx` (theme customization screen)
15. Wire up color pickers to theme atoms
16. Implement "Reset to Defaults" functionality
17. Add real-time preview

### Phase 5: Polish & Testing
18. Add animations/transitions for color changes
19. Test on iOS, Android, and Web
20. Test persistence across app restarts
21. Add user feedback (toast notifications for "Theme saved")
22. Ensure accessibility (color contrast checking, screen reader labels)
23. Add confirmation dialog for "Reset to Defaults"

---

## Technical Considerations

**HSL vs Hex:**
- Store colors as **HSL** (matches existing CSS variables)
- Convert to Hex for color picker UI
- Use `colord` library for conversions (lightweight, well-maintained)

**Cross-platform color injection:**
- **Web:** Directly modify `document.documentElement.style`
- **Native:** Use `StyleSheet.create()` with dynamic values or a theme context provider

**Performance:**
- Memoize color conversions
- Debounce color picker changes to avoid excessive re-renders
- Use Reanimated's `useSharedValue` for smooth animations

**Validation:**
- Ensure valid HSL/Hex formats
- Provide fallbacks for invalid colors
- Add color contrast warnings (e.g., "Text may be hard to read")

**Migration:**
- Gracefully handle existing `theme-preference` storage
- Migrate to new `theme-config` format on first app load after update
- Maintain backward compatibility

---

## Future Enhancements

- **Theme presets:** Pre-made color schemes (e.g., "Ocean", "Forest", "Sunset")
- **Import/export:** Share themes via JSON
- **Advanced customization:** More color slots (card, border, muted, etc.)
- **Automatic color generation:** Generate complementary colors based on a single accent
- **Dark mode variants:** Separate customization for light and dark modes
- **Live preview:** Real-time preview of theme changes on actual app content
- **Gradient backgrounds:** Support gradient background colors
- **Theme marketplace:** Community-shared themes

---

## Files to Create/Modify

**New files:**
- `lib/theme-state.ts` (or extend `lib/state.ts`)
- `lib/hooks/use-theme-injection.ts`
- `lib/utils/color-conversion.ts`
- `components/settings/color-picker.tsx`
- `components/settings/theme-preview.tsx`
- `app/settings/theme.tsx`

**Files to modify:**
- `lib/state.ts` (add new theme atoms)
- `app/_layout.tsx` (integrate theme injection hook)
- `global.css` (potentially add custom color variable placeholders)

**Dependencies to add:**
- `reanimated-color-picker`
- `colord`

---

## Notes

This plan provides a foundation for implementing a fully customizable theme system in Coral. The approach leverages the existing NativeWind + CSS variable architecture, extends the Jotai state management, and introduces a user-friendly color customization interface with cross-platform support.

The theme system integrates into the broader settings system (see `settings.md` for the overall settings page structure and navigation).
