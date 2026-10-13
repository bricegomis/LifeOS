import WidgetKit
import SwiftUI
import AppIntents

// MARK: - Timeline Provider for Next Event Widget

struct LifeosTimelineProvider: TimelineProvider {
    func placeholder(in context: Context) -> LifeosWidgetEntry {
        LifeosWidgetEntry(date: Date(), snapshot: nil, activeCooking: nil)
    }

    func getSnapshot(in context: Context, completion: @escaping (LifeosWidgetEntry) -> Void) {
        let snapshot = SharedAppGroup.loadWidgetSnapshot()
        let cooking = SharedAppGroup.loadCookingSession()
        let entry = LifeosWidgetEntry(date: Date(), snapshot: snapshot, activeCooking: cooking)
        completion(entry)
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<LifeosWidgetEntry>) -> Void) {
        let snapshot = SharedAppGroup.loadWidgetSnapshot()
        let cooking = SharedAppGroup.loadCookingSession()
        let currentDate = Date()
        
        var entries: [LifeosWidgetEntry] = []
        entries.append(LifeosWidgetEntry(date: currentDate, snapshot: snapshot, activeCooking: cooking))
        
        // Schedule future updates at the start time of upcoming events
        if let events = snapshot?.events {
            let isoFormatter = ISO8601DateFormatter()
            isoFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
            let fallbackFormatter = ISO8601DateFormatter()
            
            for event in events {
                if let iso = event.isoString,
                   let eventDate = isoFormatter.date(from: iso) ?? fallbackFormatter.date(from: iso),
                   eventDate > currentDate {
                    entries.append(LifeosWidgetEntry(date: eventDate, snapshot: snapshot, activeCooking: cooking))
                }
            }
        }
        
        // Refresh every 30 minutes minimum
        let refreshDate = Calendar.current.date(byAdding: .minute, value: 30, to: currentDate) ?? currentDate
        let timeline = Timeline(entries: entries, policy: .after(refreshDate))
        completion(timeline)
    }
}

struct LifeosWidgetEntry: TimelineEntry {
    let date: Date
    let snapshot: WidgetSnapshotDTO?
    let activeCooking: CookingSessionDTO?
}

// MARK: - Widget View

struct LifeosNextEventWidgetEntryView: View {
    var entry: LifeosWidgetEntry
    @Environment(\.widgetFamily) var family

    var nextEvent: WidgetEventItemDTO? {
        guard let events = entry.snapshot?.events else { return nil }
        let now = entry.date
        let isoFormatter = ISO8601DateFormatter()
        isoFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let fallback = ISO8601DateFormatter()
        
        let dateFormatter = DateFormatter()
        dateFormatter.dateFormat = "yyyy-MM-dd"
        let todayStr = dateFormatter.string(from: now)
        
        // 1. Look for upcoming timed events (scheduledIso/startIso >= now - 15min)
        let timedEvents = events.filter { $0.isoString != nil }
        if let upcomingTimed = timedEvents.first(where: { event in
            guard let iso = event.isoString,
                  let eventDate = isoFormatter.date(from: iso) ?? fallback.date(from: iso) else {
                return false
            }
            return eventDate >= now.addingTimeInterval(-900)
        }) {
            return upcomingTimed
        }
        
        // 2. Otherwise look for today's or future untimed events (startMinute == nil, eventDate >= todayStr)
        let untimedEvents = events.filter { $0.startMinute == nil && $0.eventDate >= todayStr }
        if let upcomingUntimed = untimedEvents.first {
            return upcomingUntimed
        }
        
        // 3. Fallback to first future event if any
        return events.first(where: { $0.eventDate >= todayStr })
    }

    var body: some View {
        switch family {
        case .accessoryRectangular:
            lockScreenRectangularView
        case .accessoryInline:
            lockScreenInlineView
        case .accessoryCircular:
            lockScreenCircularView
        default:
            homeScreenView
        }
    }
    
    // MARK: - Home Screen View
    @ViewBuilder
    private var homeScreenView: some View {
        if let cooking = entry.activeCooking, !cooking.isCompletedBool {
            cookingProgressView(cooking)
        } else if let event = nextEvent {
            eventView(event)
        } else if entry.snapshot == nil {
            emptyStateView(title: "Connexion requise", subtitle: "Ouvrez LifeOS pour synchroniser")
        } else {
            emptyStateView(title: "Aucun événement", subtitle: "Planning à jour pour la période")
        }
    }
    
    @ViewBuilder
    private func cookingProgressView(_ session: CookingSessionDTO) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Label("En cuisine", systemImage: "fork.knife")
                    .font(.caption2)
                    .fontWeight(.bold)
                    .foregroundStyle(.orange)
                Spacer()
                Text("\(session.completedStepIndices.count)/\(session.steps.count)")
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
            
            Text(session.recipeTitle)
                .font(.headline)
                .lineLimit(1)
            
            if session.steps.indices.contains(session.currentStepIndex) {
                let stepText = session.steps[session.currentStepIndex]
                HStack(alignment: .top, spacing: 8) {
                    if #available(iOS 17.0, *) {
                        Button(intent: ToggleCookingStepIntent(stepIndex: session.currentStepIndex)) {
                            Image(systemName: session.completedStepIndices.contains(session.currentStepIndex) ? "checkmark.circle.fill" : "circle")
                                .font(.title3)
                                .foregroundStyle(session.completedStepIndices.contains(session.currentStepIndex) ? .green : .primary)
                        }
                        .buttonStyle(.plain)
                    }
                    
                    Text("Étape \(session.currentStepIndex + 1) : \(stepText)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
                }
            }
            
            Spacer(minLength: 0)
        }
        .widgetURL(URL(string: "lifeos:///recipes"))
        .padding(4)
    }
    
    @ViewBuilder
    private func eventView(_ event: WidgetEventItemDTO) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Label(event.eventType == "sport" ? "Sport" : "Repas", systemImage: event.eventType == "sport" ? "figure.run" : "fork.knife")
                    .font(.caption2)
                    .fontWeight(.bold)
                    .foregroundStyle(event.eventType == "sport" ? .blue : .green)
                Spacer()
                if let startMinute = event.startMinute {
                    let h = startMinute / 60
                    let m = startMinute % 60
                    Text(String(format: "%02dh%02d", h, m))
                        .font(.caption)
                        .fontWeight(.semibold)
                } else {
                    Text("Sans horaire")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            }
            
            Text(event.title)
                .font(.headline)
                .lineLimit(2)
            
            if let sub = event.subtitle, !sub.isEmpty {
                Text(sub)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            
            Spacer(minLength: 0)
            
            Text(event.eventDate)
                .font(.caption2)
                .foregroundStyle(.tertiary)
        }
        .widgetURL(URL(string: "lifeos://" + event.deepLink))
        .padding(4)
    }
    
    @ViewBuilder
    private func emptyStateView(title: String, subtitle: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Label("LifeOS", systemImage: "calendar")
                .font(.caption2)
                .fontWeight(.bold)
                .foregroundStyle(.secondary)
            Spacer()
            Text(title)
                .font(.subheadline)
                .fontWeight(.semibold)
            Text(subtitle)
                .font(.caption2)
                .foregroundStyle(.secondary)
            Spacer()
        }
        .widgetURL(URL(string: "lifeos:///planning"))
        .padding(4)
    }
    
    // MARK: - Lock Screen
    @ViewBuilder
    private var lockScreenRectangularView: some View {
        if let event = nextEvent {
            VStack(alignment: .leading, spacing: 2) {
                HStack {
                    Image(systemName: event.eventType == "sport" ? "figure.run" : "fork.knife")
                    if let start = event.startMinute {
                        Text(String(format: "%02dh%02d", start / 60, start % 60))
                            .fontWeight(.bold)
                    } else {
                        Text("Sans horaire")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
                .font(.caption)
                Text(event.title)
                    .font(.caption)
                    .fontWeight(.semibold)
                    .lineLimit(1)
                if let sub = event.subtitle, !sub.isEmpty {
                    Text(sub)
                        .font(.caption2)
                        .lineLimit(1)
                }
            }
            .widgetURL(URL(string: "lifeos://" + event.deepLink))
        } else {
            Text("LifeOS: aucun événement")
                .font(.caption)
        }
    }
    
    @ViewBuilder
    private var lockScreenInlineView: some View {
        if let event = nextEvent {
            if let start = event.startMinute {
                let timeStr = String(format: "%02dh%02d ", start / 60, start % 60)
                Text("\(timeStr)\(event.title)")
            } else {
                Text("\(event.title) (Sans horaire)")
            }
        } else {
            Text("LifeOS à jour")
        }
    }
    
    @ViewBuilder
    private var lockScreenCircularView: some View {
        if let event = nextEvent {
            VStack(spacing: 2) {
                Image(systemName: event.eventType == "sport" ? "figure.run" : "fork.knife")
                if let start = event.startMinute {
                    Text(String(format: "%02d:%02d", start / 60, start % 60))
                        .font(.system(size: 9))
                } else {
                    Text("—")
                        .font(.system(size: 9))
                }
            }
        } else {
            Image(systemName: "calendar")
        }
    }
}

// MARK: - Widget Declaration

public struct LifeosNextEventWidget: Widget {
    public let kind: String = "LifeosNextEventWidget"

    public init() {}

    public var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: LifeosTimelineProvider()) { entry in
            LifeosNextEventWidgetEntryView(entry: entry)
                .containerBackground(.fill.tertiary, for: .widget)
        }
        .configurationDisplayName("Prochain événement LifeOS")
        .description("Affiche le prochain repas ou événement sportif, ou la recette en cours.")
        .supportedFamilies([
            .systemSmall,
            .systemMedium,
            .accessoryRectangular,
            .accessoryInline,
            .accessoryCircular
        ])
    }
}
