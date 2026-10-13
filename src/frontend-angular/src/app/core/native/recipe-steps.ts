export function splitInstructionsIntoSteps(raw: string | null | undefined): string[] {
  if (!raw) return []

  const trimmed = raw.trim()
  if (!trimmed) return []

  // Check if text has newline-separated lines
  const lines = trimmed.split(/\r?\n/).map(l => l.trim()).filter(Boolean)
  const steps: string[] = []

  for (const line of lines) {
    const cleaned = line.replace(/^(\d+[\.\)]|\*|-|\u2022)\s*/, '').trim()
    if (cleaned) {
      steps.push(cleaned)
    }
  }

  if (steps.length === 1 && lines.length === 1) {
    // If single line has inline numbered steps like "1. Faire ceci. 2. Faire cela."
    const inlineNumbered = steps[0]!.split(/(?=(?:^|\s)\d+[\.\)]\s+)/)
      .map(s => s.replace(/^\s*(\d+[\.\)]|\*|-|\u2022)\s*/, '').trim())
      .filter(Boolean)
    if (inlineNumbered.length > 1) {
      return inlineNumbered
    }
  }

  return steps.length > 0 ? steps : [trimmed]
}
