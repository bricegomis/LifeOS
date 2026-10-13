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
    
    public init() {
        self.stepIndex = 0
    }
    
    public init(stepIndex: Int) {
        self.stepIndex = stepIndex
    }
    
    public func perform() async throws -> some IntentResult {
        guard var session = SharedAppGroup.loadCookingSession() else {
            return .result()
        }
        
        var completed = Set(session.completedStepIndices)
        if completed.contains(stepIndex) {
            completed.remove(stepIndex)
        } else {
            if session.steps.indices.contains(stepIndex) {
                completed.insert(stepIndex)
            }
        }
        
        let completedArray = Array(completed).filter { session.steps.indices.contains($0) }.sorted()
        let steps = session.steps
        let nextIndex = steps.indices.first(where: { !completed.contains($0) }) ?? max(0, steps.count - 1)
        let isCompleted = !steps.isEmpty && completedArray.count == steps.count
        
        let updatedSession = CookingSessionDTO(
            sessionId: session.sessionId,
            recipeId: session.recipeId,
            recipeTitle: session.recipeTitle,
            steps: session.steps,
            completedStepIndices: completedArray,
            currentStepIndex: nextIndex,
            startedAtIso: session.startedAtIso,
            isCompleted: isCompleted
        )
        
        try? SharedAppGroup.saveCookingSession(session: updatedSession)
        
        // Update Live Activity if running with bounded counters
        let completedCount = max(0, min(completedArray.count, steps.count))
        let totalCount = steps.count
        let progress = totalCount > 0 ? Double(completedCount) / Double(totalCount) : 1.0
        let currentStepTitle = steps.indices.contains(nextIndex) ? steps[nextIndex] : "Recette terminée !"
        
        let contentState = CookingActivityAttributes.ContentState(
            currentStepIndex: nextIndex,
            currentStepTitle: currentStepTitle,
            completedCount: completedCount,
            totalCount: totalCount,
            isCompleted: isCompleted,
            progressPercent: progress
        )
        
        for activity in Activity<CookingActivityAttributes>.activities {
            await activity.update(.init(state: contentState, staleDate: Calendar.current.date(byAdding: .hour, value: 8, to: Date())))
        }
        
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}
