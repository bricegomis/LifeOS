import Foundation

public struct SharedAppGroup {
    public static var appGroupId: String {
        Bundle.main.object(forInfoDictionaryKey: "LifeosAppGroupIdentifier") as? String ?? ""
    }
    public static let widgetSnapshotFileName = "widget_snapshot.json"
    public static let cookingSessionFileName = "cooking_session.json"

    private static func fileURL(_ name: String) throws -> URL {
        guard !appGroupId.isEmpty,
              let container = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId) else {
            throw NSError(domain: "SharedAppGroup", code: 1,
                          userInfo: [NSLocalizedDescriptionKey: "App Group container not available (\(appGroupId))"])
        }
        return container.appendingPathComponent(name)
    }

    private static func coordinate<T>(url: URL, writing: Bool, block: (URL) throws -> T) throws -> T {
        let coordinator = NSFileCoordinator(filePresenter: nil)
        var error: NSError?
        var result: Result<T, Error>?
        let accessor: (URL) -> Void = { target in
            result = Result { try block(target) }
        }
        if writing {
            coordinator.coordinate(writingItemAt: url, options: [], error: &error, byAccessor: accessor)
        } else {
            coordinator.coordinate(readingItemAt: url, options: [], error: &error, byAccessor: accessor)
        }
        if let error = error { throw error }
        guard let result = result else {
            throw NSError(domain: "SharedAppGroup", code: 2, userInfo: [NSLocalizedDescriptionKey: "File coordination failed"])
        }
        return try result.get()
    }

    public static func loadWidgetSnapshot() -> WidgetSnapshotDTO? {
        do {
            let url = try fileURL(widgetSnapshotFileName)
            return try coordinate(url: url, writing: false) { target in
                guard FileManager.default.fileExists(atPath: target.path) else { return nil }
                return try JSONDecoder().decode(WidgetSnapshotDTO.self, from: Data(contentsOf: target))
            }
        } catch {
            NSLog("LifeOS widget snapshot: %@", error.localizedDescription)
            return nil
        }
    }

    public static func saveWidgetSnapshot(data: Data) throws {
        let snapshot = try JSONDecoder().decode(WidgetSnapshotDTO.self, from: data)
        guard snapshot.version == 1, !snapshot.userId.isEmpty, !snapshot.householdId.isEmpty else {
            throw NSError(domain: "SharedAppGroup", code: 3, userInfo: [NSLocalizedDescriptionKey: "Invalid widget identity/version"])
        }
        try coordinate(url: fileURL(widgetSnapshotFileName), writing: true) {
            try data.write(to: $0, options: [.atomic])
        }
    }

    private static func clearFile(_ name: String) throws {
        try coordinate(url: fileURL(name), writing: true) {
            if FileManager.default.fileExists(atPath: $0.path) { try FileManager.default.removeItem(at: $0) }
        }
    }

    public static func clearWidgetSnapshot() throws { try clearFile(widgetSnapshotFileName) }

    public static func readCookingSession() throws -> CookingSessionDTO? {
        try readCookingSession(at: fileURL(cookingSessionFileName))
    }

    static func readCookingSession(at url: URL, now: Date = Date()) throws -> CookingSessionDTO? {
        return try coordinate(url: url, writing: false) { target in
            guard FileManager.default.fileExists(atPath: target.path) else { return nil }
            let session = try JSONDecoder().decode(CookingSessionDTO.self, from: Data(contentsOf: target))
            return session.isExpired(at: now) ? nil : session
        }
    }

    public static func loadCookingSession() -> CookingSessionDTO? {
        do { return try readCookingSession() }
        catch {
            NSLog("LifeOS cooking session: %@", error.localizedDescription)
            return nil
        }
    }

    public static func saveCookingSession(data: Data) throws {
        let session = try JSONDecoder().decode(CookingSessionDTO.self, from: data)
        guard !session.isExpired() else { throw CookingContractError.expired }
        _ = try modifyCookingSession { current in
            guard current == nil || current!.isCompletedBool else { throw CookingContractError.replaced }
            return session
        }
    }

    public static func clearCookingSession(sessionId: String? = nil) throws {
        if sessionId == nil {
            try clearFile(cookingSessionFileName)
            return
        }
        _ = try modifyCookingSession { current in
            if let expected = sessionId, let current = current, expected != current.sessionId {
                throw CookingContractError.replaced
            }
            return nil
        }
    }

    public static func mutateCookingSession(sessionId: String, operation: String, stepIndex: Int?) throws -> CookingSessionDTO {
        let result = try modifyCookingSession { current in
            guard let current = current else { throw CookingContractError.expired }
            guard current.sessionId == sessionId else { throw CookingContractError.replaced }
            return try current.mutated(operation: operation, stepIndex: stepIndex)
        }
        guard let result = result else { throw CookingContractError.expired }
        return result
    }

    public static func modifyCookingSession(modify: (CookingSessionDTO?) throws -> CookingSessionDTO?) throws -> CookingSessionDTO? {
        try modifyCookingSession(at: fileURL(cookingSessionFileName), modify: modify)
    }

    // The URL overload also exercises real coordinated IO without requiring signing in tests.
    static func modifyCookingSession(at url: URL, modify: (CookingSessionDTO?) throws -> CookingSessionDTO?) throws -> CookingSessionDTO? {
        try coordinate(url: url, writing: true) { target in
            var current: CookingSessionDTO?
            if FileManager.default.fileExists(atPath: target.path) {
                let decoded = try JSONDecoder().decode(CookingSessionDTO.self, from: Data(contentsOf: target))
                current = decoded.isExpired() ? nil : decoded
            }
            let updated = try modify(current)
            if let updated = updated {
                try JSONEncoder().encode(updated).write(to: target, options: [.atomic])
            } else if FileManager.default.fileExists(atPath: target.path) {
                try FileManager.default.removeItem(at: target)
            }
            return updated
        }
    }
}

public struct WidgetSnapshotDTO: Codable {
    public let version: Int
    public let householdId: String
    public let userId: String
    public let generatedAt: String
    public let timeZoneId: String
    public let events: [WidgetEventItemDTO]
}

public struct WidgetEventItemDTO: Codable, Identifiable {
    public let id: String
    public let kind: String
    public let title: String
    public let date: String
    public let startMinute: Int?
    public let endMinute: Int?
    public let startIso: String?
    public let subtitle: String?
    public let deepLink: String
    public var eventType: String { kind }
    public var eventDate: String { date }
    public var isoString: String? { startIso }
}
