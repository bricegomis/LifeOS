import { inject, Injectable } from '@angular/core'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { TasksApiService } from './tasks-api.service'
import type { TodoDto } from './tasks.models'

/** Ticking a todo from any screen: done (with "Annuler" in the message) or reopened. */
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
}
