import { ScrollViewStyleReset } from 'expo-router/html';
import { type PropsWithChildren } from 'react';

// This file is web-only and used to configure the root HTML for every
// web page during static rendering.
// The contents of this function only run in Node.js environments and
// do not have access to the DOM or browser APIs.
export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="en" className="bg-background">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />

        {/* Prevent theme flash by setting background color immediately based on stored preference */}
        <script dangerouslySetInnerHTML={{
          __html: `
            (function() {
              try {
                var themePreference = localStorage.getItem('theme-preference');
                if (themePreference) {
                  themePreference = JSON.parse(themePreference);
                }

                var theme = 'dark'; // default to dark
                var backgroundColor = '#09090b';

                if (themePreference === 'light') {
                  theme = 'light';
                  backgroundColor = '#ffffff';
                } else if (themePreference === 'dark') {
                  theme = 'dark';
                  backgroundColor = '#09090b';
                } else {
                  // system theme - check media query
                  if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) {
                    theme = 'light';
                    backgroundColor = '#ffffff';
                  }
                }

                // Set class and style on html element (body doesn't exist yet)
                document.documentElement.classList.add(theme);
                document.documentElement.style.backgroundColor = backgroundColor;
              } catch (e) {
                // If anything fails, default to dark
                document.documentElement.classList.add('dark');
                document.documentElement.style.backgroundColor = '#09090b';
              }
            })();
          `
        }} />

        {/*
          Disable body scrolling on web. This makes ScrollView components work closer to how they do on native.
          However, body scrolling is often nice to have for mobile web. If you want to enable it, remove this line.
        */}
        <ScrollViewStyleReset />

        {/* Add any additional <head> elements that you want globally available on web... */}
      </head>
      <body>
        {children}
        {/* Set body background after it exists */}
        <script dangerouslySetInnerHTML={{
          __html: `
            (function() {
              try {
                var bgColor = document.documentElement.style.backgroundColor;
                if (bgColor) {
                  document.body.style.backgroundColor = bgColor;
                }
              } catch (e) {}
            })();
          `
        }} />
      </body>
    </html>
  );
}
