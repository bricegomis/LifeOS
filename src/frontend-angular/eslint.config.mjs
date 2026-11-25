import angular from 'angular-eslint'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [...tseslint.configs.recommended, ...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      // A leading underscore marks a parameter kept on purpose (fakes, callbacks).
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
  },
  {
    ignores: ['dist/**', 'node_modules/**', 'ios/DerivedData/**', 'ios/App/Pods/**', 'ios/App/App/public/**'],
  },
)
