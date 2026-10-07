import { Component } from '@angular/core'
import { PlanningPageComponent } from '@/app/pages/planning/planning-page.component'

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [PlanningPageComponent],
  template: '<app-planning-page [todayOnly]="true" />',
})
export class DashboardPageComponent {}
