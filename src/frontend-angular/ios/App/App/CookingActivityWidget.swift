import WidgetKit
import SwiftUI
import ActivityKit
import AppIntents

public struct CookingActivityWidget: Widget {
    public init() {}
    
    public var body: some WidgetConfiguration {
        ActivityConfiguration(for: CookingActivityAttributes.self) { context in
            // Lock Screen / Banner presentation
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Label(context.attributes.recipeTitle, systemImage: "fork.knife")
                        .font(.headline)
                        .foregroundStyle(.primary)
                    Spacer()
                    Text("\(context.state.completedCount)/\(context.state.totalCount)")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                
                // Current step
                HStack(alignment: .top, spacing: 8) {
                    if #available(iOS 17.0, *) {
                        Button(intent: ToggleCookingStepIntent(stepIndex: context.state.currentStepIndex, sessionId: context.attributes.sessionId)) {
                            Image(systemName: context.state.isCompleted ? "checkmark.circle.fill" : "circle")
                                .font(.title2)
                                .foregroundStyle(context.state.isCompleted ? .green : .orange)
                        }
                        .buttonStyle(.plain)
                    }
                    
                    VStack(alignment: .leading, spacing: 2) {
                        Text(context.state.isCompleted ? "Terminé !" : "Étape \(context.state.currentStepIndex + 1)")
                            .font(.caption)
                            .fontWeight(.bold)
                            .foregroundStyle(.secondary)
                        Text(context.state.currentStepTitle)
                            .font(.body)
                            .lineLimit(2)
                    }
                }
                
                // Progress Bar
                ProgressView(value: context.state.progressPercent)
                    .tint(.orange)
            }
            .padding()
            .widgetURL(URL(string: "lifeos:///recipes"))
        } dynamicIsland: { context in
            DynamicIsland {
                // Expanded
                DynamicIslandExpandedRegion(.leading) {
                    Label(context.attributes.recipeTitle, systemImage: "fork.knife")
                        .font(.caption)
                        .lineLimit(1)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    Text("\(context.state.completedCount)/\(context.state.totalCount)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    HStack {
                        if #available(iOS 17.0, *) {
                            Button(intent: ToggleCookingStepIntent(stepIndex: context.state.currentStepIndex, sessionId: context.attributes.sessionId)) {
                                Image(systemName: context.state.isCompleted ? "checkmark.circle.fill" : "circle")
                                    .foregroundStyle(context.state.isCompleted ? .green : .orange)
                            }
                            .buttonStyle(.plain)
                        }
                        Text(context.state.currentStepTitle)
                            .font(.caption)
                            .lineLimit(1)
                    }
                }
            } compactLeading: {
                Image(systemName: "fork.knife")
                    .foregroundStyle(.orange)
            } compactTrailing: {
                Text("\(context.state.completedCount)/\(context.state.totalCount)")
                    .font(.caption2)
            } minimal: {
                Image(systemName: "fork.knife")
                    .foregroundStyle(.orange)
            }
            .widgetURL(URL(string: "lifeos:///recipes"))
        }
    }
}
