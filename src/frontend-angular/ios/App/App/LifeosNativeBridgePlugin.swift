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
        CAPPluginMethod(name: "mutateCookingSession", returnType: CAPPluginReturnPromise),
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
        do {
            if let session = try SharedAppGroup.readCookingSession() {
                let encoder = JSONEncoder()
                let data = try encoder.encode(session)
                guard let dict = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] else {
                    throw NSError(domain: "LifeosNativeBridge", code: 1, userInfo: [NSLocalizedDescriptionKey: "Invalid cooking encoding"])
                }
                call.resolve(["session": dict])
                return
            }
            call.resolve(["session": NSNull()])
        } catch {
            call.reject("Failed to read cooking session: \(error.localizedDescription)")
        }
    }

    @objc func mutateCookingSession(_ call: CAPPluginCall) {
        guard let sessionId = call.getString("sessionId"),
              let operation = call.getString("operation") else {
            call.reject("Missing cooking mutation parameters")
            return
        }
        do {
            let session = try SharedAppGroup.mutateCookingSession(
                sessionId: sessionId, operation: operation, stepIndex: call.getInt("stepIndex")
            )
            let data = try JSONEncoder().encode(session)
            guard let dict = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                throw NSError(domain: "LifeosNativeBridge", code: 1, userInfo: [NSLocalizedDescriptionKey: "Invalid cooking encoding"])
            }
            WidgetCenter.shared.reloadAllTimelines()
            call.resolve(["session": dict])
        } catch {
            call.reject("Failed cooking mutation: \(error.localizedDescription)")
        }
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
            try SharedAppGroup.clearCookingSession(sessionId: call.getString("sessionId"))
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
        
        let session: CookingSessionDTO
        do {
            guard let current = try SharedAppGroup.readCookingSession(),
                  current.sessionId == call.getString("sessionId") else { throw CookingContractError.expired }
            session = current
        } catch {
            call.reject(error.localizedDescription)
            return
        }
        let steps = session.steps
        let completedIndices = session.completedStepIndices
        let currentStepIndex = session.currentStepIndex
        let currentStepTitle = steps.indices.contains(currentStepIndex) ? steps[currentStepIndex] : "Terminé"
        let isCompleted = completedIndices.count == steps.count && !steps.isEmpty
        let progress = steps.isEmpty ? 1.0 : Double(completedIndices.count) / Double(steps.count)
        
        // Terminate any existing activities first
        Task {
            for activity in Activity<CookingActivityAttributes>.activities {
                await activity.end(nil, dismissalPolicy: .immediate)
            }
            
            let attributes = CookingActivityAttributes(
                sessionId: session.sessionId,
                recipeTitle: session.recipeTitle,
                startedAt: CookingSessionDTO.date(session.startedAt)!
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
                    content: .init(state: state, staleDate: session.expiresAt),
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
        
        let currentSession: CookingSessionDTO
        do {
            guard let current = try SharedAppGroup.readCookingSession() else { throw CookingContractError.expired }
            currentSession = current
        } catch {
            call.reject(error.localizedDescription)
            return
        }
        let completedIndices = currentSession.completedStepIndices
        let isCompleted = currentSession.isCompletedBool
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
                if activity.attributes.sessionId == currentSession.sessionId && !isCompleted {
                    await activity.update(.init(state: updatedState, staleDate: currentSession.expiresAt))
                } else {
                    await activity.end(nil, dismissalPolicy: .immediate)
                }
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
