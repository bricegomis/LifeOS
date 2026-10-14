import { readFileSync } from 'node:fs'
import { validateIosConfig } from './ios-config.mjs'

const source = readFileSync('dist/browser/config.js', 'utf8')
const match = source.match(/^window\.LIFEOS_CONFIG = (.*);\s*$/)
if (!match) throw new Error('iOS : config.js généré non reconnu')
validateIosConfig(JSON.parse(match[1]))
console.log('Configuration publique du bundle iOS validée (connectivité réelle non vérifiée).')
