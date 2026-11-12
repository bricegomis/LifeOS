import type { BuildInfo } from '../build-info'

export interface BuildInfoDto extends BuildInfo {
  component: 'api'
}

export interface SportTemplateRequest {
  name: string
  sport: string
  durationMinutes: number
  distanceKm: number | null
  intensity: 'low' | 'moderate' | 'high'
  calories: number
}

export interface SportTemplateDto extends SportTemplateRequest {
  id: string
  householdId: string
  isArchived: boolean
}

/** An assistant (Claude) allowed to act on LifeOS through the MCP endpoint (ADR 0011). */
export interface AssistantConnectionDto {
  id: string
  clientName: string
  grantedAt: string | null
}

export interface MemberProfileDto {
  id: string
  displayName: string
  portionCoefficient: number
  createdAt: string
  updatedAt: string
}

export interface ReviewDto {
  id: string
  foodItemId: string | null
  recipeId: string | null
  memberProfileId: string
  profileName: string | null
  score: number | null
  comment: string | null
  createdByUserId: string
  updatedAt: string
}

export interface SaveReviewRequest {
  memberProfileId: string
  score: number | null
  comment: string | null
}
