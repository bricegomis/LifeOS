import Foundation

public struct SharedAppGroup {
    public static let appGroupId = "group.fr.lifeos.app"
    public static let widgetSnapshotFileName = "widget_snapshot.json"
    public static let cookingSessionFileName = "cooking_session.json"
    
    private static var containerURL: URL? {
        return FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId)
    }
    
    // MARK: - Snapshot
    public static func loadWidgetSnapshot() -> WidgetSnapshotDTO? {
        guard let url = containerURL?.appendingPathComponent(widgetSnapshotFileName),
              FileManager.default.fileExists(atPath: url.path) else {
            return nil
        }
        do {
            let data = try Data(contentsOf: url)
            let decoder = JSONDecoder()
            return try decoder.decode(WidgetSnapshotDTO.self, from: data)
        } catch {
            NSLog("SharedAppGroup: Failed to load widget snapshot: %@", error.localizedDescription)
            return nil
        }
    }
    
    public static func saveWidgetSnapshot(data: Data) throws {
        guard let url = containerURL?.appendingPathComponent(widgetSnapshotFileName) else {
            throw NSError(domain: "SharedAppGroup", code: 1, userInfo: [NSLocalizedDescriptionKey: "App Group container not available"])
        }
        try data.write(to: url, options: [.atomic])
    }
    
    public static func clearWidgetSnapshot() {
        guard let url = containerURL?.appendingPathComponent(widgetSnapshotFileName) else { return }
        try? FileManager.default.removeItem(at: url)
    }
    
    // MARK: - Cooking Session
    public static func loadCookingSession() -> CookingSessionDTO? {
        guard let url = containerURL?.appendingPathComponent(cookingSessionFileName),
              FileManager.default.fileExists(atPath: url.path) else {
            return nil
        }
        do {
            let data = try Data(contentsOf: url)
            let decoder = JSONDecoder()
            return try decoder.decode(CookingSessionDTO.self, from: data)
        } catch {
            NSLog("SharedAppGroup: Failed to load cooking session: %@", error.localizedDescription)
            return nil
        }
    }
    
    public static func saveCookingSession(data: Data) throws {
        guard let url = containerURL?.appendingPathComponent(cookingSessionFileName) else {
            throw NSError(domain: "SharedAppGroup", code: 1, userInfo: [NSLocalizedDescriptionKey: "App Group container not available"])
        }
        try data.write(to: url, options: [.atomic])
    }
    
    public static func saveCookingSession(session: CookingSessionDTO) throws {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        let data = try encoder.encode(session)
        try saveCookingSession(data: data)
    }
    
    public static func clearCookingSession() {
        guard let url = containerURL?.appendingPathComponent(cookingSessionFileName) else { return }
        try? FileManager.default.removeItem(at: url)
    }
}

// MARK: - DTO Models matching TypeScript native.models.ts

public struct WidgetSnapshotDTO: Codable {
    public let version: Int
    public let householdId: String
    public let userDisplayName: String
    public let generatedAt: String
    public let timeZoneId: String
    public let events: [WidgetEventItemDTO]
    public let activeCookingSession: CookingSessionDTO?
}

public struct WidgetEventItemDTO: Codable, Identifiable {
    public let id: String
    public let type: String // "meal" | "sport"
    public let title: String
    public let targetDate: String // YYYY-MM-DD
    public let startMinute: Int?
    public let endMinute: Int?
    public let scheduledIso: String?
    public let subtitle: String?
    public let deepLink: String
}

public struct CookingSessionDTO: Codable {
    public let sessionId: String
    public let recipeId: String
    public let recipeTitle: String
    public let steps: [String]
    public let completedStepIndices: [Int]
    public let currentStepIndex: Int
    public let startedAtIso: String
    public let isCompleted: boolean_wrapper? // In JSON might be isCompleted: Bool
    public let isCompletedBool: Bool
    
    enum CodingKeys: String, CodingKey {
        case sessionId
        case recipeId
        case recipeTitle
        case steps
        case completedStepIndices
        case currentStepIndex
        case startedAtIso
        case isCompleted
    }
    
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        sessionId = try container.decode(String.self, forKey: .sessionId)
        recipeId = try container.decode(String.self, forKey: .recipeId)
        recipeTitle = try container.decode(String.self, forKey: .recipeTitle)
        steps = try container.decode([String].self, forKey: .steps)
        completedStepIndices = try container.decode([Int].self, forKey: .completedStepIndices)
        currentStepIndex = try container.decode(Int.self, forKey: .currentStepIndex)
        startedAtIso = try container.decode(String.self, forKey: .startedAtIso)
        isCompletedBool = try container.decodeIfPresent(Bool.self, forKey: .isCompleted) ?? false
        isCompleted = nil
    }
    
    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(sessionId, forKey: .sessionId)
        try container.encode(recipeId, forKey: .recipeId)
        try container.encode(recipeTitle, forKey: .recipeTitle)
        try container.encode(steps, forKey: .steps)
        try container.encode(completedStepIndices, forKey: .completedStepIndices)
        try container.encode(currentStepIndex, forKey: .currentStepIndex)
        try container.encode(startedAtIso, forKey: .startedAtIso)
        try container.encode(isCompletedBool, forKey: .isCompleted)
    }
    
    public init(
        sessionId: String,
        recipeId: String,
        recipeTitle: String,
        steps: [String],
        completedStepIndices: [Int],
        currentStepIndex: Int,
        startedAtIso: String,
        isCompleted: Bool
    ) {
        self.sessionId = sessionId
        self.recipeId = recipeId
        self.recipeTitle = recipeTitle
        self.steps = steps
        self.completedStepIndices = completedStepIndices
        self.currentStepIndex = currentStepIndex
        self.startedAtIso = startedAtIso
        self.isCompletedBool = isCompleted
        self.isCompleted = nil
    }
}

public struct boolean_wrapper: Codable {}
