import Foundation

do {
    if CommandLine.arguments.count == 3 && CommandLine.arguments[1] == "--read" {
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
