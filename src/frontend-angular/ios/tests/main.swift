import Foundation

do {
    if CommandLine.arguments.count == 2 && CommandLine.arguments[1] == "--timeline-test" {
        let now = Date(timeIntervalSince1970: 100)
        let expiry = now.addingTimeInterval(8 * 3600)
        precondition(cookingTimelineDates(now: now, eventDates: [], expiresAt: expiry) == [now, expiry])
        precondition(cookingTimelineDates(now: now, eventDates: [expiry, expiry, now], expiresAt: expiry) == [now, expiry])
        let event = expiry.addingTimeInterval(10)
        precondition(cookingTimelineDates(now: now, eventDates: [event, event], expiresAt: expiry) == [now, expiry, event])
        precondition(cookingTimelineDates(now: expiry, eventDates: [], expiresAt: expiry) == [expiry])
    } else if CommandLine.arguments.count == 3 && CommandLine.arguments[1] == "--read" {
        let session = try SharedAppGroup.readCookingSession(at: URL(fileURLWithPath: CommandLine.arguments[2]))
        FileHandle.standardOutput.write(try JSONEncoder().encode(session))
    } else if CommandLine.arguments.count == 4 && CommandLine.arguments[1] == "--toggle" {
        let url = URL(fileURLWithPath: CommandLine.arguments[2])
        let index = Int(CommandLine.arguments[3])!
        _ = try SharedAppGroup.modifyCookingSession(at: url) { current in
            guard let current = current else { throw CookingContractError.expired }
            return try current.mutated(operation: "toggle", stepIndex: index)
        }
    } else {
        let data = FileHandle.standardInput.readDataToEndOfFile()
        let session = try JSONDecoder().decode(CookingSessionDTO.self, from: data)
        if CommandLine.arguments.count == 3 && CommandLine.arguments[1] == "--expired-at" {
            guard let now = CookingSessionDTO.date(CommandLine.arguments[2]) else { throw CookingContractError.expired }
            FileHandle.standardOutput.write(Data(String(session.isExpired(at: now)).utf8))
        } else {
            FileHandle.standardOutput.write(try JSONEncoder().encode(session))
        }
    }
} catch {
    FileHandle.standardError.write(Data(error.localizedDescription.utf8))
    exit(1)
}
