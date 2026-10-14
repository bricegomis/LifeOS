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
        guard let expectedId = sessionId else { throw CookingContractError.replaced }
        let updatedSession = try SharedAppGroup.mutateCookingSession(
            sessionId: expectedId, operation: "toggle", stepIndex: stepIndex
        )
        
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
                    await activity.update(.init(state: contentState, staleDate: updatedSession.expiresAt))
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
