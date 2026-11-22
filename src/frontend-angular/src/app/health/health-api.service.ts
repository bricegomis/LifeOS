import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type {
  AnalysisDto, AnalysisRequest, MarkerDto, MarkerListDto, MarkerRequest, MeasureTypeDto,
  CheckinRequest, SupplementDto, SupplementListDto, SupplementRequest, SymptomDayDto, SymptomEntryDto, SymptomEntryRequest, SymptomJournalDto, SymptomKind, SymptomTypeDto,
} from './health.models'

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

  /** The journal of the last `days` days and the symptom list (created with the defaults the first time). */
  symptoms(days = 30): Observable<SymptomJournalDto> { return this.api.get<SymptomJournalDto>('/health/symptoms', { days }) }
  createSymptomType(name: string, kind: SymptomKind): Observable<SymptomTypeDto> { return this.api.post<SymptomTypeDto>('/health/symptoms/types', { name, kind }) }
  archiveSymptomType(typeId: string, restore = false): Observable<SymptomTypeDto> {
    return this.api.post<SymptomTypeDto>(`/health/symptoms/types/${id(typeId)}/archive${restore ? '?restore=true' : ''}`, {})
  }
  addSymptom(request: SymptomEntryRequest): Observable<SymptomEntryDto> { return this.api.post<SymptomEntryDto>('/health/symptoms/entries', request) }
  updateSymptom(entryId: string, request: SymptomEntryRequest): Observable<SymptomEntryDto> {
    return this.api.put<SymptomEntryDto>(`/health/symptoms/entries/${id(entryId)}`, request)
  }
  removeSymptom(entryId: string): Observable<void> { return this.api.delete(`/health/symptoms/entries/${id(entryId)}`) }
  checkin(request: CheckinRequest): Observable<SymptomDayDto> { return this.api.post<SymptomDayDto>('/health/symptoms/checkin', request) }

  markers(includeArchived = false): Observable<MarkerListDto> { return this.api.get<MarkerListDto>('/health/markers', { includeArchived }) }
  marker(markerId: string): Observable<MarkerDto> { return this.api.get<MarkerDto>(`/health/markers/${id(markerId)}`) }
  createMarker(request: MarkerRequest): Observable<MarkerDto> { return this.api.post<MarkerDto>('/health/markers', request) }
  updateMarker(markerId: string, request: MarkerRequest): Observable<MarkerDto> { return this.api.put<MarkerDto>(`/health/markers/${id(markerId)}`, request) }
  archiveMarker(markerId: string, restore = false): Observable<MarkerDto> {
    return this.api.post<MarkerDto>(`/health/markers/${id(markerId)}/archive${restore ? '?restore=true' : ''}`, {})
  }
  analyses(): Observable<AnalysisDto[]> { return this.api.get<AnalysisDto[]>('/health/analyses') }
  analysis(analysisId: string): Observable<AnalysisDto> { return this.api.get<AnalysisDto>(`/health/analyses/${id(analysisId)}`) }
  createAnalysis(request: AnalysisRequest): Observable<AnalysisDto> { return this.api.post<AnalysisDto>('/health/analyses', request) }
  updateAnalysis(analysisId: string, request: AnalysisRequest): Observable<AnalysisDto> { return this.api.put<AnalysisDto>(`/health/analyses/${id(analysisId)}`, request) }
  removeAnalysis(analysisId: string): Observable<void> { return this.api.delete(`/health/analyses/${id(analysisId)}`) }
  /** The body measures with their values (created with a default list the first time). */
  measures(): Observable<MeasureTypeDto[]> { return this.api.get<MeasureTypeDto[]>('/health/measures') }
  createMeasureType(name: string, unit: string): Observable<MeasureTypeDto> { return this.api.post<MeasureTypeDto>('/health/measures/types', { name, unit }) }
  archiveMeasureType(typeId: string, restore = false): Observable<MeasureTypeDto> {
    return this.api.post<MeasureTypeDto>(`/health/measures/types/${id(typeId)}/archive${restore ? '?restore=true' : ''}`, {})
  }
  addMeasurement(measureTypeId: string, value: number, day: string | null, note: string | null): Observable<MeasureTypeDto> {
    return this.api.post<MeasureTypeDto>('/health/measures/values', { measureTypeId, day, value, note })
  }
  removeMeasurement(measurementId: string): Observable<void> { return this.api.delete(`/health/measures/values/${id(measurementId)}`) }
}
