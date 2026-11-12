const TINTS = ['#efc9a6', '#f0d59c', '#e3ddd2', '#f0cdb8', '#efe6cf', '#e6e9e2', '#ecd9a8', '#efc4b8', '#e9cf9a', '#f2c9a0', '#f1e0a6', '#d9d6d0']

/** A stable warm tint per name, used where no photo exists yet. */
export function tintFor(name: string): string {
  let hash = 0
  for (const char of name) hash = (hash * 31 + char.codePointAt(0)!) >>> 0
  return TINTS[hash % TINTS.length]!
}
