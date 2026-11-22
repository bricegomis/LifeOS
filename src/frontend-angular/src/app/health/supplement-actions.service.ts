import { inject, Injectable } from '@angular/core'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { HealthApiService } from './health-api.service'
import type { SupplementDto } from './health.models'

/** Ticking a supplement as taken today from any screen. */
@Injectable({ providedIn: 'root' })
export class SupplementActionsService {
  private readonly api = inject(HealthApiService)
  private readonly snack = inject(MatSnackBar)

  async toggle(supplement: SupplementDto): Promise<SupplementDto | null> {
    try {
      const updated = await firstValueFrom(this.api.setTaken(supplement.id, !supplement.takenToday))
      this.snack.open(updated.takenToday ? `« ${supplement.name} » pris aujourd’hui` : `« ${supplement.name} » décoché`, undefined, { duration: 3000 })
      return updated
    } catch (error) {
      this.snack.open(apiErrorMessage(error), undefined, { duration: 6000 })
      return null
    }
  }
}
