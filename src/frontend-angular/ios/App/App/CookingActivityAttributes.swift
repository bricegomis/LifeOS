import Foundation
import ActivityKit

public struct CookingActivityAttributes: ActivityAttributes {
    public struct ContentState: Codable, Hashable {
        public var currentStepIndex: Int
        public var currentStepTitle: String
        public var completedCount: Int
        public var totalCount: Int
        public var isCompleted: Bool
        public var progressPercent: Double
        
        public init(
            currentStepIndex: Int,
            currentStepTitle: String,
            completedCount: Int,
            totalCount: Int,
            isCompleted: Bool,
            progressPercent: Double
        ) {
            self.currentStepIndex = currentStepIndex
            self.currentStepTitle = currentStepTitle
            self.completedCount = completedCount
            self.totalCount = totalCount
            self.isCompleted = isCompleted
            self.progressPercent = progressPercent
        }
    }
    
    // Fixed non-changing attributes
    public var sessionId: String
    public var recipeTitle: String
    public var startedAt: Date
    
    public init(sessionId: String, recipeTitle: String, startedAt: Date) {
        self.sessionId = sessionId
        self.recipeTitle = recipeTitle
        self.startedAt = startedAt
    }
}
