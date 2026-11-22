import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type { SupplementDto, SupplementListDto, SupplementRequest } from './health.models'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the Health screens. The API only ever returns the caller's own health data. */
@Injectable({ providedIn: 'root' })
export class HealthApiService {
  private readonly api = inject(LifeosApiService)

  supplements(includeStopped = false): Observable<SupplementListDto> { return this.api.get<SupplementListDto>('/health/supplements', { includeStopped }) }
  supplement(supplementId: string): Observable<SupplementDto> { return this.api.get<SupplementDto>(`/health/supplements/${id(supplementId)}`) }
  createSupplement(request: SupplementRequest): Observable<SupplementDto> { return this.api.post<SupplementDto>('/health/supplements', request) }
  updateSupplement(supplementId: string, request: SupplementRequest): Observable<SupplementDto> {
    return this.api.put<SupplementDto>(`/health/supplements/${id(supplementId)}`, request)
  }
  /** Ticks (or unticks) a day; today when `day` is null. */
  setTaken(supplementId: string, taken: boolean, day: string | null = null): Observable<SupplementDto> {
    return this.api.post<SupplementDto>(`/health/supplements/${id(supplementId)}/intakes`, { day, taken })
  }
  stopSupplement(supplementId: string): Observable<SupplementDto> { return this.api.post<SupplementDto>(`/health/supplements/${id(supplementId)}/stop`, {}) }
  resumeSupplement(supplementId: string): Observable<SupplementDto> { return this.api.post<SupplementDto>(`/health/supplements/${id(supplementId)}/resume`, {}) }
  uploadSupplementImage(supplementId: string, file: File): Observable<SupplementDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<SupplementDto>(`/health/supplements/${id(supplementId)}/image`, form)
    }))
  }
  removeSupplementImage(supplementId: string): Observable<void> { return this.api.delete(`/health/supplements/${id(supplementId)}/image`) }
}
