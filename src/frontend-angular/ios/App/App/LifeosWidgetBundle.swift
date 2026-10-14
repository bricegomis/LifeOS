import WidgetKit
import SwiftUI

@main
struct LifeosWidgetBundle: WidgetBundle {
    var body: some Widget {
        LifeosNextEventWidget()
        CookingActivityWidget()
    }
}
