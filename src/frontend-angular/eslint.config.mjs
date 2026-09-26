import angular from 'angular-eslint'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
  },
  {
    ignores: ['dist/**', 'node_modules/**'],
  },
)
