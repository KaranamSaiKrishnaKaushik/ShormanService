import { Component } from '@angular/core';
import { DatePipe, NgFor } from '@angular/common';

interface RiderTask {
  id: number;
  customer: string;
  slot: string;
  area: string;
  status: 'Assigned' | 'Picked Up' | 'Delivered';
}

@Component({
  selector: 'app-rider-dashboard',
  standalone: true,
  imports: [NgFor, DatePipe],
  template: `
    <section class="rider-shell">
      <header class="hero">
        <div>
          <span class="eyebrow">Rider Workspace</span>
          <h1>Assigned deliveries and route status in one place.</h1>
          <p>This area is reserved for riders and Super Admin. The live delivery workflow can expand here without mixing with customer screens.</p>
        </div>
        <div class="meta-card">
          <span>{{ today | date: 'EEEE, d MMM y' }}</span>
          <strong>{{ tasks.length }} active stops</strong>
        </div>
      </header>

      <div class="task-grid">
        <article *ngFor="let task of tasks" class="task-card">
          <div class="task-top">
            <div>
              <h2>{{ task.customer }}</h2>
              <p>{{ task.area }}</p>
            </div>
            <span class="status">{{ task.status }}</span>
          </div>
          <div class="task-meta">
            <span>Stop #{{ task.id }}</span>
            <span>{{ task.slot }}</span>
          </div>
        </article>
      </div>
    </section>
  `,
  styles: [`
    .rider-shell {
      padding: 2rem;
      max-width: 1100px;
      margin: 0 auto;
    }
    .hero {
      display: grid;
      grid-template-columns: 1.8fr 1fr;
      gap: 1.25rem;
      align-items: stretch;
      margin-bottom: 1.5rem;
    }
    .eyebrow {
      display: inline-block;
      color: #8b4d00;
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      margin-bottom: 0.75rem;
    }
    h1 {
      margin: 0 0 0.75rem;
      font-size: clamp(2rem, 4vw, 3.1rem);
      line-height: 1.05;
      color: #132f24;
    }
    p {
      margin: 0;
      color: #53695a;
      line-height: 1.6;
    }
    .meta-card,
    .task-card {
      background: linear-gradient(180deg, #ffffff 0%, #f7fbf5 100%);
      border: 1px solid #dce6d8;
      border-radius: 22px;
      box-shadow: 0 18px 38px rgba(24, 54, 44, 0.08);
    }
    .meta-card {
      padding: 1.4rem;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      color: #365042;
    }
    .meta-card strong {
      font-size: 2rem;
      color: #174d2b;
    }
    .task-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1rem;
    }
    .task-card {
      padding: 1.2rem;
    }
    .task-top {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
      margin-bottom: 1rem;
    }
    .task-top h2 {
      margin: 0 0 0.35rem;
      font-size: 1.15rem;
      color: #20352a;
    }
    .task-top p {
      margin: 0;
      font-size: 0.95rem;
    }
    .status {
      background: #eef7ed;
      color: #1f6a35;
      border-radius: 999px;
      padding: 0.45rem 0.75rem;
      font-size: 0.82rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .task-meta {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      color: #64796c;
      font-size: 0.92rem;
    }
    @media (max-width: 820px) {
      .hero {
        grid-template-columns: 1fr;
      }
      .rider-shell {
        padding: 1.25rem;
      }
    }
  `]
})
export class RiderDashboardComponent {
  today = new Date();
  tasks: RiderTask[] = [
    { id: 101, customer: 'Marta Klein', slot: '10:00 - 10:30', area: 'Berlin Mitte', status: 'Assigned' },
    { id: 102, customer: 'Jonas Richter', slot: '11:00 - 11:30', area: 'Prenzlauer Berg', status: 'Picked Up' },
    { id: 103, customer: 'Sofia Weber', slot: '12:30 - 13:00', area: 'Friedrichshain', status: 'Delivered' }
  ];
}