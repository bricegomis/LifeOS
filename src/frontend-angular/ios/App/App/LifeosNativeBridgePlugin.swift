import Foundation
import Capacitor
import WidgetKit
import ActivityKit

@objc(LifeosNativeBridgePlugin)
public class LifeosNativeBridgePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "LifeosNativeBridgePlugin"
    public let jsName = "LifeosNativeBridge"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "saveWidgetSnapshot", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "clearWidgetSnapshot", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "getCookingSession", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "saveCookingSession", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "clearCookingSession", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "startLiveActivity", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "updateLiveActivity", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "endLiveActivity", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "reloadWidgetTimelines", returnType: CAPPluginReturnPromise)
    ]
    
    // MARK: - Snapshot
    @objc func saveWidgetSnapshot(_ call: CAPPluginCall) {
        guard let snapshotObj = call.getObject("snapshot") else {
            call.reject("Missing snapshot object")
            return
        }
        
        do {
            let data = try JSONSerialization.data(withJSONObject: snapshotObj, options: [.prettyPrinted])
            try SharedAppGroup.saveWidgetSnapshot(data: data)
            WidgetCenter.shared.reloadAllTimelines()
            call.resolve()
        } catch {
            call.reject("Failed to save snapshot: \(error.localizedDescription)")
        }
    }
    
    @objc func clearWidgetSnapshot(_ call: CAPPluginCall) {
        do {
            try SharedAppGroup.clearWidgetSnapshot()
            WidgetCenter.shared.reloadAllTimelines()
            call.resolve()
        } catch {
            call.reject("Failed to clear snapshot: \(error.localizedDescription)")
        }
    }
    
    // MARK: - Cooking Session
    @objc func getCookingSession(_ call: CAPPluginCall) {
        if let session = SharedAppGroup.loadCookingSession() {
            do {
                let encoder = JSONEncoder()
                let data = try encoder.encode(session)
                if let dict = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] {
                    call.resolve(["session": dict])
                    return
                }
            } catch {
                NSLog("Failed to encode cooking session: %@", error.localizedDescription)
            }
        }
        call.resolve(["session": NSNull()])
    }
    
    @objc func saveCookingSession(_ call: CAPPluginCall) {
        guard let sessionObj = call.getObject("session") else {
            call.reject("Missing session object")
            return
        }
        
        do {
            let data = try JSONSerialization.data(withJSONObject: sessionObj, options: [.prettyPrinted])
            try SharedAppGroup.saveCookingSession(data: data)
            WidgetCenter.shared.reloadAllTimelines()
            call.resolve()
        } catch {
            call.reject("Failed to save cooking session: \(error.localizedDescription)")
        }
    }
    
    @objc func clearCookingSession(_ call: CAPPluginCall) {
        do {
            try SharedAppGroup.clearCookingSession()
            WidgetCenter.shared.reloadAllTimelines()
            call.resolve()
        } catch {
            call.reject("Failed to clear cooking session: \(error.localizedDescription)")
        }
    }
    
    // MARK: - Live Activities
    @objc func startLiveActivity(_ call: CAPPluginCall) {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else {
            call.resolve(["activityId": NSNull(), "disabled": true])
            return
        }
        
        guard let sessionId = call.getString("sessionId"),
              let recipeTitle = call.getString("recipeTitle"),
              let steps = call.getArray("steps", String.self) else {
            call.reject("Missing parameters for startLiveActivity")
            return
        }
        
        let completedIndices = call.getArray("completedStepIndices", Int.self) ?? []
        let currentStepIndex = steps.indices.first(where: { !completedIndices.contains($0) }) ?? (steps.count - 1)
        let currentStepTitle = steps.indices.contains(currentStepIndex) ? steps[currentStepIndex] : "Terminé"
        let isCompleted = completedIndices.count == steps.count && !steps.isEmpty
        let progress = steps.isEmpty ? 1.0 : Double(completedIndices.count) / Double(steps.count)
        
        // Terminate any existing activities first
        Task {
            for activity in Activity<CookingActivityAttributes>.activities {
                await activity.end(nil, dismissalPolicy: .immediate)
            }
            
            let attributes = CookingActivityAttributes(
                sessionId: sessionId,
                recipeTitle: recipeTitle,
                startedAt: Date()
            )
            
            let state = CookingActivityAttributes.ContentState(
                currentStepIndex: currentStepIndex,
                currentStepTitle: currentStepTitle,
                completedCount: completedIndices.count,
                totalCount: steps.count,
                isCompleted: isCompleted,
                progressPercent: progress
            )
            
            do {
                let activity = try Activity<CookingActivityAttributes>.request(
                    attributes: attributes,
                    content: .init(state: state, staleDate: Calendar.current.date(byAdding: .hour, value: 8, to: Date())),
                    pushType: nil
                )
                call.resolve(["activityId": activity.id])
            } catch {
                call.reject("Failed to start Live Activity: \(error.localizedDescription)")
            }
        }
    }
    
    @objc func updateLiveActivity(_ call: CAPPluginCall) {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else {
            call.resolve()
            return
        }
        
        let completedIndices = call.getArray("completedStepIndices", Int.self) ?? []
        let isCompleted = call.getBool("isCompleted") ?? false
        
        guard let currentSession = SharedAppGroup.loadCookingSession() else {
            call.resolve()
            return
        }
        
        let steps = currentSession.steps
        let currentStepIndex = steps.indices.first(where: { !completedIndices.contains($0) }) ?? (steps.count - 1)
        let currentStepTitle = steps.indices.contains(currentStepIndex) ? steps[currentStepIndex] : "Recette terminée !"
        let progress = steps.isEmpty ? 1.0 : Double(completedIndices.count) / Double(steps.count)
        
        let updatedState = CookingActivityAttributes.ContentState(
            currentStepIndex: currentStepIndex,
            currentStepTitle: currentStepTitle,
            completedCount: completedIndices.count,
            totalCount: steps.count,
            isCompleted: isCompleted,
            progressPercent: progress
        )
        
        Task {
            for activity in Activity<CookingActivityAttributes>.activities {
                await activity.update(.init(state: updatedState, staleDate: Calendar.current.date(byAdding: .hour, value: 8, to: Date())))
            }
            call.resolve()
        }
    }
    
    @objc func endLiveActivity(_ call: CAPPluginCall) {
        Task {
            for activity in Activity<CookingActivityAttributes>.activities {
                await activity.end(nil, dismissalPolicy: .immediate)
            }
            call.resolve()
        }
    }
    
    // MARK: - Widgets
    @objc func reloadWidgetTimelines(_ call: CAPPluginCall) {
        WidgetCenter.shared.reloadAllTimelines()
        call.resolve()
    }
}
