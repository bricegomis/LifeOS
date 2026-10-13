import { Injectable, signal, computed } from '@angular/core'
import type { CookingSessionState } from './native.models'
import { LifeosNativeBridge } from './native-bridge'
import { splitInstructionsIntoSteps } from './recipe-steps'

const LOCAL_STORAGE_KEY = 'lifeos.native.cookingSession'

@Injectable({ providedIn: 'root' })
export class CookingSessionService {
  readonly currentSession = signal<CookingSessionState | null>(null)
  readonly hasActiveSession = computed(() => {
    const s = this.currentSession()
    return Boolean(s && !s.isCompleted)
  })

  constructor() {
    void this.restoreSession()
  }

  async restoreSession(): Promise<CookingSessionState | null> {
    try {
      const res = await LifeosNativeBridge.getCookingSession()
      if (res.session) {
        this.currentSession.set(res.session)
        return res.session
      }
    } catch {
      // Fallback to localStorage if bridge failed
    }

    if (typeof localStorage !== 'undefined') {
      const raw = localStorage.getItem(LOCAL_STORAGE_KEY)
      if (raw) {
        try {
          const parsed = JSON.parse(raw) as CookingSessionState
          this.currentSession.set(parsed)
          return parsed
        } catch {
          // ignore parsing error
        }
      }
    }

    this.currentSession.set(null)
    return null
  }

  async startSession(recipeTitle: string, recipeId: string | null, rawInstructions: string | null): Promise<CookingSessionState> {
    const steps = splitInstructionsIntoSteps(rawInstructions)
    const session: CookingSessionState = {
      sessionId: `cook-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
      recipeId,
      recipeTitle: recipeTitle.trim() || 'Recette',
      startedAt: new Date().toISOString(),
      steps,
      completedStepIndices: [],
      isCompleted: false,
      updatedAt: new Date().toISOString(),
    }

    this.currentSession.set(session)
    await this.persistAndSync(session)

    try {
      await LifeosNativeBridge.startLiveActivity({
        sessionId: session.sessionId,
        recipeTitle: session.recipeTitle,
        steps: session.steps,
        completedStepIndices: session.completedStepIndices,
      })
    } catch {
      // Live activity might not be supported or allowed
    }

    return session
  }

  async toggleStep(stepIndex: number): Promise<void> {
    const current = this.currentSession()
    if (!current) return

    const completed = new Set(current.completedStepIndices)
    if (completed.has(stepIndex)) {
      completed.delete(stepIndex)
    } else {
      completed.add(stepIndex)
    }

    const updatedIndices = Array.from(completed).sort((a, b) => a - b)
    const allCompleted = current.steps.length > 0 && updatedIndices.length === current.steps.length

    const updatedSession: CookingSessionState = {
      ...current,
      completedStepIndices: updatedIndices,
      isCompleted: allCompleted,
      updatedAt: new Date().toISOString(),
    }

    this.currentSession.set(updatedSession)
    await this.persistAndSync(updatedSession)

    try {
      await LifeosNativeBridge.updateLiveActivity({
        completedStepIndices: updatedSession.completedStepIndices,
        isCompleted: updatedSession.isCompleted,
      })
      if (allCompleted) {
        await LifeosNativeBridge.endLiveActivity()
      }
    } catch {
      // ignore bridge errors
    }
  }

  async completeSession(): Promise<void> {
    const current = this.currentSession()
    if (!current) return

    const updatedSession: CookingSessionState = {
      ...current,
      completedStepIndices: current.steps.map((_, i) => i),
      isCompleted: true,
      updatedAt: new Date().toISOString(),
    }

    this.currentSession.set(updatedSession)
    await this.persistAndSync(updatedSession)

    try {
      await LifeosNativeBridge.endLiveActivity()
    } catch {
      // ignore
    }
  }

  async cancelSession(): Promise<void> {
    this.currentSession.set(null)
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(LOCAL_STORAGE_KEY)
    }
    try {
      await LifeosNativeBridge.clearCookingSession()
      await LifeosNativeBridge.endLiveActivity()
      await LifeosNativeBridge.reloadWidgetTimelines()
    } catch {
      // ignore
    }
  }

  private async persistAndSync(session: CookingSessionState): Promise<void> {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(session))
    }
    try {
      await LifeosNativeBridge.saveCookingSession({ session })
      await LifeosNativeBridge.reloadWidgetTimelines()
    } catch {
      // ignore
    }
  }
}
