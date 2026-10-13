import AppIntents
import ActivityKit
import WidgetKit
import Foundation

@available(iOS 17.0, *)
public struct ToggleCookingStepIntent: AppIntent, LiveActivityIntent {
    public static var title: LocalizedStringResource = "Cocher ou décocher une étape de cuisine"
    public static var description: IntentDescription = IntentDescription("Met à jour l'étape courante de cuisine LifeOS")
    
    @Parameter(title: "Index de l'étape")
    public var stepIndex: Int
    
    @Parameter(title: "ID de session attendu")
    public var sessionId: String?
    
    public init() {
        self.stepIndex = 0
        self.sessionId = nil
    }
    
    public init(stepIndex: Int, sessionId: String? = nil) {
        self.stepIndex = stepIndex
        self.sessionId = sessionId
    }
    
    public func perform() async throws -> some IntentResult {
        let expectedId = self.sessionId
        let targetIndex = self.stepIndex
        
        let modified = try SharedAppGroup.modifyCookingSession { currentSession in
            guard let session = currentSession else {
                throw NSError(domain: "ToggleCookingStepIntent", code: 404, userInfo: [NSLocalizedDescriptionKey: "Aucune session de cuisine active"])
            }
            
            // Guard against stale actions targeting a previous session
            if let expected = expectedId, expected != session.sessionId {
                throw NSError(domain: "ToggleCookingStepIntent", code: 409, userInfo: [NSLocalizedDescriptionKey: "Session obsolète ou remplacée"])
            }
            
            guard session.steps.indices.contains(targetIndex) else {
                throw NSError(domain: "ToggleCookingStepIntent", code: 400, userInfo: [NSLocalizedDescriptionKey: "Index d'étape invalide"])
            }
            
            var completed = Set(session.completedStepIndices)
            if completed.contains(targetIndex) {
                completed.remove(targetIndex)
            } else {
                completed.insert(targetIndex)
            }
            
            let completedArray = Array(completed).filter { session.steps.indices.contains($0) }.sorted()
            let steps = session.steps
            let nextIndex = steps.indices.first(where: { !completed.contains($0) }) ?? max(0, steps.count - 1)
            let isCompleted = !steps.isEmpty && completedArray.count == steps.count
            
            return CookingSessionDTO(
                sessionId: session.sessionId,
                recipeId: session.recipeId,
                recipeTitle: session.recipeTitle,
                steps: session.steps,
                completedStepIndices: completedArray,
                currentStepIndex: nextIndex,
                startedAtIso: session.startedAtIso,
                isCompleted: isCompleted
            )
        }
        
        guard let updatedSession = modified else {
            return .result()
        }
        
        // Update Live Activity if running with bounded counters
        let steps = updatedSession.steps
        let completedCount = max(0, min(updatedSession.completedStepIndices.count, steps.count))
        let totalCount = steps.count
        let progress = totalCount > 0 ? Double(completedCount) / Double(totalCount) : 1.0
        let currentStepTitle = steps.indices.contains(updatedSession.currentStepIndex)
            ? steps[updatedSession.currentStepIndex]
            : "Recette terminée !"
        
        let contentState = CookingActivityAttributes.ContentState(
            currentStepIndex: updatedSession.currentStepIndex,
            currentStepTitle: currentStepTitle,
            completedCount: completedCount,
            totalCount: totalCount,
            isCompleted: updatedSession.isCompletedBool,
            progressPercent: progress
        )
        
        for activity in Activity<CookingActivityAttributes>.activities {
            // Reconcile if activity belongs to this session
            if activity.attributes.sessionId == updatedSession.sessionId {
                if updatedSession.isCompletedBool {
                    await activity.end(.init(state: contentState, staleDate: nil), dismissalPolicy: .after(Date().addingTimeInterval(15)))
                } else {
                    await activity.update(.init(state: contentState, staleDate: Calendar.current.date(byAdding: .hour, value: 8, to: Date())))
                }
            } else {
                // Obsolete activity from old session: terminate
                await activity.end(nil, dismissalPolicy: .immediate)
            }
        }
        
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}
