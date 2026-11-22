import { inject, Injectable } from '@angular/core'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { TasksApiService } from './tasks-api.service'
import type { TaskRoutineDetailDto, TaskRoutineDto, TodoDto } from './tasks.models'

/** Ticking a todo or a routine from any screen, with "Annuler" in the message. */
@Injectable({ providedIn: 'root' })
export class TodoActionsService {
  private readonly api = inject(TasksApiService)
  private readonly snack = inject(MatSnackBar)

  /** Returns the updated todo, or null after an error (already shown). `changed` runs again after an undo. */
  async toggle(todo: TodoDto, changed: () => void): Promise<TodoDto | null> {
    try {
      if (todo.state === 'done') {
        const reopened = await firstValueFrom(this.api.reopen(todo.id))
        this.snack.open(`« ${todo.title} » rouvert`, undefined, { duration: 4000 })
        return reopened
      }
      const done = await firstValueFrom(this.api.complete(todo.id))
      this.snack.open(`« ${todo.title} » fait`, 'Annuler', { duration: 6000 }).onAction().subscribe(async () => {
        try { await firstValueFrom(this.api.reopen(todo.id)); changed() }
        catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
      })
      return done
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
      return null
    }
  }

  /** Marks a routine done (today unless `day`) or skips it; "Annuler" removes the new pass. */
  async closeRoutine(routine: TaskRoutineDto, how: 'done' | 'skip', changed: () => void, day: string | null = null, note: string | null = null)
    : Promise<TaskRoutineDetailDto | null> {
    try {
      const detail = await firstValueFrom(how === 'done' ? this.api.routineDone(routine.id, day, note) : this.api.skipRoutine(routine.id))
      const pass = detail.passes.find(p => p.kind === (how === 'done' ? 'done' : 'skipped'))
      const message = how === 'done' ? `« ${routine.title} » fait` : `« ${routine.title} » sauté, série remise à zéro`
      this.snack.open(message, pass ? 'Annuler' : undefined, { duration: 6000 }).onAction().subscribe(async () => {
        try { await firstValueFrom(this.api.removePass(routine.id, pass!.id)); changed() }
        catch (error) { this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 }) }
      })
      return detail
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
      return null
    }
  }
}
