/** SVG files are bundled as text (angular.json `loader`), for the icon registry. */
declare module '*.svg' {
  const content: string
  export default content
}
