module.exports = {
  extends: [
    'eslint:recommended',
    'next/core-web-vitals',
    'plugin:react/recommended',
    'plugin:react/jsx-runtime',
    'plugin:@typescript-eslint/recommended',
    'prettier',
  ],
  rules: {
    'react/self-closing-comp': ['warn', { component: true, html: true }],
    'react/jsx-curly-brace-presence': ['warn', { props: 'never', children: 'never' }],
    '@typescript-eslint/no-unused-vars': [
      'error',
      {
        // unused variables must be marked with _ prefix
        argsIgnorePattern: '^_',
        caughtErrorsIgnorePattern: '_',
        destructuredArrayIgnorePattern: '^_',
        varsIgnorePattern: '^_',
        ignoreRestSiblings: true,
      },
    ],
    '@typescript-eslint/ban-types': [
      'error',
      {
        types: {
          // allow empty object literal type
          '{}': false,
        },
      },
    ],
  }
};
