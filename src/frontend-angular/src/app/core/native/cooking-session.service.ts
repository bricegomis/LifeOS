import { Injectable, signal, computed } from '@angular/core'
import { Capacitor } from '@capacitor/core'
import type { CookingSessionState } from './native.models'
import { LifeosNativeBridge, requireNativeBridge } from './native-bridge'
import { splitInstructionsIntoSteps } from './recipe-steps'
import { COOKING_STORAGE_KEY, readCookingSession } from './cooking-contract'
import { nativeStateCoordinator, SupersededNativeOperation } from './native-state-coordinator'

@Injectable({ providedIn: 'root' })
export class CookingSessionService {
  readonly currentSession = signal<CookingSessionState | null>(null)
  readonly lastError = signal<string | null>(null)
  readonly liveActivityStatus = signal<'web' | 'available' | 'disabled' | 'error'>(
    Capacitor.isNativePlatform() ? 'available' : 'web',
  )
  readonly hasActiveSession = computed(() => Boolean(this.currentSession() && !this.currentSession()?.isCompleted))

  clearLocalState(): void {
    this.currentSession.set(null)
    this.lastError.set(null)
    if (typeof localStorage !== 'undefined') localStorage.removeItem(COOKING_STORAGE_KEY)
  }

  async restoreSession(): Promise<CookingSessionState | null> {
    return this.perform(async () => {
      const res = await LifeosNativeBridge.getCookingSession()
      const session = readCookingSession(res.session)
      // The native file is authoritative: never resurrect a separate JS cache.
      if (!session && res.session) {
        await LifeosNativeBridge.clearCookingSession({ sessionId: res.session.sessionId })
      }
      if (!session) await LifeosNativeBridge.endLiveActivity()
      return session
    })
  }

  async startSession(recipeTitle: string, recipeId: string | null, rawInstructions: string | null): Promise<CookingSessionState> {
    const steps = splitInstructionsIntoSteps(rawInstructions)
    if (!steps.length) throw new Error('Ajoutez au moins une étape de cuisine')
    const session: CookingSessionState = {
      version: 2, revision: 0,
      sessionId: `cook-${crypto.randomUUID()}`,
      recipeId, recipeTitle: recipeTitle.trim() || 'Recette',
      startedAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
      steps, completedStepIndices: [], isCompleted: false,
    }
    await this.perform(async () => {
      await LifeosNativeBridge.saveCookingSession({ session })
      await this.syncLiveActivity(session, true)
      await LifeosNativeBridge.reloadWidgetTimelines()
      return session
    })
    return session
  }

  async toggleStep(stepIndex: number): Promise<void> {
    await this.mutate('toggle', stepIndex)
  }

  async completeSession(): Promise<void> {
    await this.mutate('complete')
  }

  async cancelSession(): Promise<void> {
    const sessionId = this.currentSession()?.sessionId
    await this.perform(async () => {
      await LifeosNativeBridge.clearCookingSession({ sessionId })
      await LifeosNativeBridge.endLiveActivity()
      await LifeosNativeBridge.reloadWidgetTimelines()
      return null
    })
  }

  private async mutate(operation: 'toggle' | 'complete', stepIndex?: number): Promise<void> {
    const sessionId = this.currentSession()?.sessionId
    if (!sessionId) throw new Error('Aucune session cuisine active')
    await this.perform(async () => {
      const { session } = await LifeosNativeBridge.mutateCookingSession({ sessionId, operation, stepIndex })
      await this.syncLiveActivity(session)
      await LifeosNativeBridge.reloadWidgetTimelines()
      return session
    })
  }

  private async syncLiveActivity(session: CookingSessionState, start = false): Promise<void> {
    if (!Capacitor.isNativePlatform()) return
    try {
      if (start) {
        const result = await LifeosNativeBridge.startLiveActivity(session)
        this.liveActivityStatus.set(result.disabled ? 'disabled' : 'available')
      } else if (session.isCompleted) {
        await LifeosNativeBridge.endLiveActivity()
      } else {
        await LifeosNativeBridge.updateLiveActivity(session)
      }
    } catch (error) {
      this.liveActivityStatus.set('error')
      this.lastError.set(`Live Activity indisponible : ${error instanceof Error ? error.message : String(error)}`)
    }
  }

  private async perform(operation: () => Promise<CookingSessionState | null>): Promise<CookingSessionState | null> {
    const token = nativeStateCoordinator.token()
    this.lastError.set(null)
    try {
      const session = await nativeStateCoordinator.run(token, async () => {
        requireNativeBridge()
        return operation()
      })
      this.currentSession.set(session)
      return session
    } catch (error) {
      if (!(error instanceof SupersededNativeOperation)) {
        this.lastError.set(error instanceof Error ? error.message : 'Erreur du pont cuisine')
      }
      throw error
    }
  }
}
