/**
 * Beeps, vibration and screen wake lock for the guided run of a routine. Browsers only play sound after a
 * tap: `unlock()` is called from the button that starts a countdown. Everything fails silently when the
 * device does not support it (no sound, no vibration, the screen may lock).
 */
export class RoutineSignals {
  private context: AudioContext | null = null
  private wakeLock: { released?: boolean, release(): Promise<void> } | null = null
  private wanted = false
  private readonly onVisible = () => { if (this.wanted && document.visibilityState === 'visible') void this.lock() }

  /** Creates or resumes the audio context and keeps the screen on; call it from a user tap. */
  unlock(): void {
    void this.keepAwake()
    try {
      const AudioContextClass = globalThis.AudioContext ?? (globalThis as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
      if (!AudioContextClass) return
      this.context ??= new AudioContextClass()
      if (this.context.state === 'suspended') void this.context.resume()
    } catch { /* no sound */ }
  }

  /** A short beep for each of the last seconds. */
  tick(): void { this.tone(880, 0.12) }

  /** The hold starts: one longer beep. */
  go(): void { this.tone(1320, 0.35) }

  /** The end of a hold: two longer, higher beeps and a vibration. */
  done(): void {
    this.tone(1320, 0.25)
    this.tone(1320, 0.45, 0.32)
    try { navigator.vibrate?.([200, 100, 300]) } catch { /* no vibration */ }
  }

  /** Keeps the screen on while the run is open, and again when the page comes back. */
  async keepAwake(): Promise<void> {
    this.wanted = true
    // The same listener is added only once.
    document.addEventListener('visibilitychange', this.onVisible)
    await this.lock()
  }

  release(): void {
    this.wanted = false
    document.removeEventListener('visibilitychange', this.onVisible)
    void this.wakeLock?.release().catch(() => undefined)
    this.wakeLock = null
    void this.context?.close().catch(() => undefined)
    this.context = null
  }

  private async lock(): Promise<void> {
    if (this.wakeLock && !this.wakeLock.released) return
    try {
      const wakeLock = (navigator as { wakeLock?: { request(type: 'screen'): Promise<{ released?: boolean, release(): Promise<void> }> } }).wakeLock
      if (wakeLock) this.wakeLock = await wakeLock.request('screen')
    } catch { /* the screen may lock */ }
  }

  private tone(frequency: number, duration: number, delay = 0): void {
    const context = this.context
    if (!context) return
    try {
      const start = context.currentTime + delay
      const oscillator = context.createOscillator(), gain = context.createGain()
      oscillator.type = 'sine'
      oscillator.frequency.value = frequency
      gain.gain.setValueAtTime(0.0001, start)
      gain.gain.exponentialRampToValueAtTime(0.6, start + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, start + duration)
      oscillator.connect(gain).connect(context.destination)
      oscillator.start(start)
      oscillator.stop(start + duration + 0.05)
    } catch { /* no sound */ }
  }
}
