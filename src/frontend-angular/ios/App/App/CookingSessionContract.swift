import Foundation

public struct CookingSessionDTO: Codable {
    public static let maximumAge: TimeInterval = 8 * 60 * 60
    public let version: Int
    public let revision: Int
    public let sessionId: String
    public let recipeId: String?
    public let recipeTitle: String
    public let steps: [String]
    public let completedStepIndices: [Int]
    public let startedAt: String
    public let updatedAt: String
    public var isCompletedBool: Bool { !steps.isEmpty && completedStepIndices.count == steps.count }
    public var currentStepIndex: Int {
        steps.indices.first(where: { !completedStepIndices.contains($0) }) ?? max(0, steps.count - 1)
    }
    public var startedAtIso: String { startedAt }
    public var expiresAt: Date { Self.date(startedAt)!.addingTimeInterval(Self.maximumAge) }

    public static func date(_ value: String) -> Date? {
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return fractional.date(from: value) ?? ISO8601DateFormatter().date(from: value)
    }

    public func isExpired(at now: Date = Date()) -> Bool {
        guard let start = Self.date(startedAt) else { return true }
        return now < start || now >= expiresAt
    }

    enum CodingKeys: String, CodingKey {
        case version, revision, sessionId, recipeId, recipeTitle, steps, completedStepIndices
        case startedAt, startedAtIso, updatedAt, isCompleted
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        let version = try c.decodeIfPresent(Int.self, forKey: .version) ?? 2
        let revision = try c.decodeIfPresent(Int.self, forKey: .revision) ?? 0
        let startedAt = try c.decodeIfPresent(String.self, forKey: .startedAt)
            ?? c.decode(String.self, forKey: .startedAtIso)
        let updatedAt = try c.decodeIfPresent(String.self, forKey: .updatedAt) ?? startedAt
        let sessionId = try c.decode(String.self, forKey: .sessionId)
        guard version == 2, revision >= 0, revision < 9_007_199_254_740_991,
              !sessionId.isEmpty, Self.date(startedAt) != nil, Self.date(updatedAt) != nil else {
            throw DecodingError.dataCorrupted(.init(codingPath: c.codingPath, debugDescription: "Invalid cooking contract"))
        }
        self.version = version
        self.revision = revision
        self.sessionId = sessionId
        self.recipeId = try c.decodeIfPresent(String.self, forKey: .recipeId)
        self.recipeTitle = try c.decode(String.self, forKey: .recipeTitle)
        let steps = try c.decode([String].self, forKey: .steps)
        self.steps = steps
        let indices = try c.decode([Int].self, forKey: .completedStepIndices)
        self.completedStepIndices = Array(Set(indices.filter { $0 >= 0 && $0 < steps.count })).sorted()
        self.startedAt = startedAt
        self.updatedAt = updatedAt
    }

    public func encode(to encoder: Encoder) throws {
        var c = encoder.container(keyedBy: CodingKeys.self)
        try c.encode(version, forKey: .version)
        try c.encode(revision, forKey: .revision)
        try c.encode(sessionId, forKey: .sessionId)
        try c.encodeIfPresent(recipeId, forKey: .recipeId)
        try c.encode(recipeTitle, forKey: .recipeTitle)
        try c.encode(steps, forKey: .steps)
        try c.encode(completedStepIndices, forKey: .completedStepIndices)
        try c.encode(startedAt, forKey: .startedAt)
        try c.encode(updatedAt, forKey: .updatedAt)
        try c.encode(isCompletedBool, forKey: .isCompleted)
    }

    public func mutated(operation: String, stepIndex: Int?, at now: Date = Date()) throws -> CookingSessionDTO {
        guard !isExpired(at: now) else { throw CookingContractError.expired }
        guard revision < 9_007_199_254_740_990 else { throw CookingContractError.invalidOperation }
        var completed = Set(completedStepIndices)
        switch operation {
        case "toggle":
            guard let index = stepIndex, steps.indices.contains(index) else { throw CookingContractError.invalidIndex }
            if completed.contains(index) { completed.remove(index) } else { completed.insert(index) }
        case "complete": completed = Set(steps.indices)
        default: throw CookingContractError.invalidOperation
        }
        return CookingSessionDTO(previous: self, indices: Array(completed).sorted(), at: now)
    }

    private init(previous: CookingSessionDTO, indices: [Int], at now: Date) {
        version = 2
        revision = previous.revision + 1
        sessionId = previous.sessionId
        recipeId = previous.recipeId
        recipeTitle = previous.recipeTitle
        steps = previous.steps
        completedStepIndices = indices
        startedAt = previous.startedAt
        updatedAt = ISO8601DateFormatter().string(from: now)
    }
}

public enum CookingContractError: LocalizedError {
    case expired, replaced, invalidIndex, invalidOperation
    public var errorDescription: String? {
        switch self {
        case .expired: return "Session cuisine absente ou expirée"
        case .replaced: return "Session cuisine remplacée : actualisez avant de réessayer"
        case .invalidIndex: return "Index cuisine invalide"
        case .invalidOperation: return "Mutation cuisine invalide"
        }

    }
}

public func cookingTimelineDates(now: Date, eventDates: [Date], expiresAt: Date?) -> [Date] {
    var dates = Set(eventDates.filter { $0 > now })
    dates.insert(now)
    if let expiry = expiresAt, expiry > now { dates.insert(expiry) }
    return dates.sorted()
}
