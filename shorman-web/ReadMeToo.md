The right first step is not rider-specific push notifications. Since this is a web app, the safest version is an in-app real-time rider queue that all riders can see, backed by persistent order state. Admins can also see the same stream. Later, if needed, you can add browser push or mobile push, but that should be phase 2, not phase 1.

**Recommended Flow**
1. Customer places an order.
2. The order enters a shared `Awaiting Pickup` rider queue.
3. All riders see it in their notification pane or rider dashboard.
4. One rider clicks `Accept / Pick Up`.
5. The system atomically assigns that order to that rider and removes it from the common queue for other riders.
6. The rider opens the order, sees item list, store, customer address, payment type, and can click `Open in Google Maps`.
7. The rider moves the order through statuses: `Picked Up` → `Out for Delivery` → `Delivered`.
8. If payment method is `Cash on Delivery`, the rider must confirm `Cash Collected` before the order can become `Completed`.
9. Until the order reaches its terminal state, it stays visible in the rider’s active notifications/orders pane.
10. Admin can always see all active rider orders and intervene if needed.

**Why This Is The Right Plan**
For a web-only system, true device-targeted notifications are weak unless you add browser push, service workers, and permission handling. That is extra complexity and still not as reliable as a mobile app. A shared real-time queue is much simpler and matches your current stage.

Use this order of implementation:
1. In-app real-time notifications for all riders and admins.
2. Rider accepts an order from the shared queue.
3. Persistent rider task panel with statuses and map link.
4. Optional browser push notifications later.
5. Mobile app much later if operationally needed.

**What To Build**
Use one persistent workflow, not temporary notifications only.

Core order states:
1. `Pending`
2. `AwaitingPickup`
3. `AssignedToRider`
4. `PickedUp`
5. `OutForDelivery`
6. `Delivered`
7. `Completed`
8. `Cancelled`

Payment states:
1. `Pending`
2. `Paid`
3. `CashPending`
4. `CashCollected`

Recommended completion rule:
1. Online payment: allow `Completed` after rider marks `Delivered`.
2. Cash on delivery: allow `Completed` only after `Delivered` and `CashCollected`.

**Notification Strategy**
Treat notifications as a view of active orders, not as the source of truth.

That means:
1. The order status in the database is the truth.
2. The notification pane shows orders where status is not terminal.
3. When an order is completed or cancelled, it disappears from active notifications automatically.
4. Keep an audit/event log for history.

This avoids “notification gone but order still active” problems.

**Backend Plan**
Add these fields to orders or a delivery table:
1. `AssignedRiderId`
2. `DeliveryStatus`
3. `PaymentStatus`
4. `AcceptedAtUtc`
5. `PickedUpAtUtc`
6. `DeliveredAtUtc`
7. `CompletedAtUtc`

Add an order event log:
1. `OrderCreated`
2. `OrderAssigned`
3. `PickupStarted`
4. `OutForDelivery`
5. `Delivered`
6. `CashCollected`
7. `Completed`

Add APIs:
1. `GET /api/rider/orders/available`
2. `GET /api/rider/orders/mine`
3. `GET /api/rider/orders/{id}`
4. `POST /api/rider/orders/{id}/accept`
5. `POST /api/rider/orders/{id}/picked-up`
6. `POST /api/rider/orders/{id}/out-for-delivery`
7. `POST /api/rider/orders/{id}/delivered`
8. `POST /api/rider/orders/{id}/cash-collected`
9. `POST /api/rider/orders/{id}/complete`

Important rule:
`accept` must be atomic, so two riders cannot claim the same order.

**Frontend Plan**
For riders:
1. Add a `Rider Notifications` or `Rider Orders` panel.
2. Split it into:
   1. `Available Orders`
   2. `My Active Deliveries`
   3. `Completed History`
3. Clicking an order opens full detail:
   1. Customer address
   2. Store/supermarket
   3. Full item list
   4. Payment method
   5. Order notes
   6. `Open in Google Maps`

Google Maps link format:
`https://www.google.com/maps/search/?api=1&query=<encoded address>`

For admins:
1. Show all active orders.
2. Show whether unassigned or assigned.
3. Allow reassignment if needed.

**Real-Time Delivery Of Notifications**
Best current choice: SignalR.

Plan:
1. Customer places order.
2. Backend updates order status to `AwaitingPickup`.
3. Backend emits SignalR event to rider group and admin group.
4. Open rider/admin dashboards update instantly.
5. If SignalR disconnects, fallback polling every 15-30 seconds.

This gives you web notifications without needing Android/iOS.

**How Your 4 Requirements Map**
1. Customer raises order, admin gets notifications:
Yes. Admin and riders can both receive real-time in-app notifications. Start with a shared rider queue, not individual rider targeting.

2. Rider sees a common notification, picks up, opens address in Maps, delivers, completes:
Yes. This should be the main flow for phase 1.

3. If not completed, notification stays visible; clicking it shows full order details and store:
Yes. The notification pane should be driven by active order state, so it persists until completion.

4. Cash on delivery only completes after delivery and cash collection:
Yes. Make `Complete` depend on:
`Delivered == true` and, if COD, `CashCollected == true`.

**Suggested Implementation Phases**
1. Phase 1: Delivery workflow foundation
Create order delivery statuses, rider acceptance, order detail page, and admin visibility.

2. Phase 2: Real-time rider/admin notifications
Add SignalR updates and fallback polling.

3. Phase 3: Delivery completion rules
Enforce COD collection before completion and keep audit trail.

4. Phase 4: Browser notifications
Optional. Add browser Notification API only after the in-app flow is stable.

**Recommendation**
Do not start with “send to a particular rider.” Start with:
1. shared rider queue,
2. first rider accepts,
3. system assigns and locks,
4. rider completes delivery flow.

That is the simplest model, operationally clear, and fits a web app well.

If you want, the next step can be a concrete implementation plan broken into backend tables, APIs, Angular screens, and status transitions in exact build order.