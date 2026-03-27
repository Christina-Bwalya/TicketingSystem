# External Helpdesk Integration — API Reference

> Share this document with the external helpdesk team.

---

## Base URL

```
https://<your-railway-domain>
```

## Authentication

All requests must include this header:

```
X-Helpdesk-Secret: <shared_secret>
```

The shared secret will be provided separately via a secure channel.

---

## Endpoint 1 — Create a Ticket in Christina's System

Use this when you have a ticket with category **Network**, **Account**, or **Access** that should be handled by Christina's team.

```
POST /api/helpdesk/tickets
Content-Type: application/json
X-Helpdesk-Secret: <shared_secret>
```

### Request Body

```json
{
  "external_id": "EXT-5678",
  "title": "User cannot access VPN",
  "description": "User reports VPN connection drops every 5 minutes.",
  "category": "Network",
  "priority": "Medium",
  "created_by": "helpdesk.agent@external.com",
  "created_date": "2025-03-26T11:00:00Z"
}
```

| Field          | Type     | Required | Notes                                      |
|----------------|----------|----------|--------------------------------------------|
| `external_id`  | string   | Yes      | Your internal ticket ID                    |
| `title`        | string   | Yes      | Max 100 characters                         |
| `description`  | string   | Yes      | Max 1000 characters                        |
| `category`     | string   | Yes      | Must be: `Network`, `Account`, or `Access` |
| `priority`     | string   | Yes      | `Low`, `Medium`, `High`, or `Critical`     |
| `created_by`   | string   | Yes      | Agent name or email                        |
| `created_date` | datetime | Yes      | ISO 8601 UTC                               |

### Response — 201 Created

```json
{
  "local_id": 99,
  "local_url": "https://<your-railway-domain>/api/tickets/99"
}
```

Store `local_id` — you will need it to send updates.

---

## Endpoint 2 — Send a Status or Comment Update

Use this when a ticket you received from Christina's system (Software, Hardware, Other) has a status change or a new comment.

```
POST /api/helpdesk/webhook
Content-Type: application/json
X-Helpdesk-Secret: <shared_secret>
```

### Status Update

```json
{
  "external_id": "EXT-1234",
  "event_type": "status_update",
  "new_status": "in_progress",
  "timestamp": "2025-03-26T12:00:00Z"
}
```

### Comment Added

```json
{
  "external_id": "EXT-1234",
  "event_type": "comment_added",
  "comment_author": "John Tech",
  "comment_message": "We have identified the issue and are working on a fix.",
  "timestamp": "2025-03-26T12:30:00Z"
}
```

| Field             | Type     | Required                          |
|-------------------|----------|-----------------------------------|
| `external_id`     | string   | Yes — ID we sent you on forwarding|
| `event_type`      | string   | Yes — `status_update` or `comment_added` |
| `new_status`      | string   | Required for `status_update`      |
| `comment_author`  | string   | Required for `comment_added`      |
| `comment_message` | string   | Required for `comment_added`      |
| `timestamp`       | datetime | Yes — ISO 8601 UTC                |

### Valid Status Values

| Send this       | Maps to          |
|-----------------|------------------|
| `open`          | Open             |
| `in_progress`   | In Progress      |
| `waiting`       | Waiting for User |
| `resolved`      | Resolved         |
| `closed`        | Closed           |

### Response — 200 OK

```json
{ "message": "Update applied." }
```

---

## What Christina's System Sends You (Outbound)

When a ticket is created in Christina's system with category **Software**, **Hardware**, or **Other**, we will POST to your API:

```json
{
  "local_id": 42,
  "title": "Laptop not booting",
  "description": "The laptop shows a black screen on startup.",
  "category": "Hardware",
  "priority": "High",
  "created_by": "jane.doe",
  "created_date": "2025-03-26T10:00:00Z",
  "callback_webhook_url": "https://<your-railway-domain>/api/helpdesk/webhook",
  "callback_secret": "<shared_secret>"
}
```

**Your system must respond with:**

```json
{
  "external_id": "EXT-1234"
}
```

We store this `external_id` to match your future updates back to the correct ticket.

---

## Error Responses

| HTTP Code | Meaning                                      |
|-----------|----------------------------------------------|
| 400       | Invalid category, missing fields, unknown event type |
| 401       | Missing or incorrect `X-Helpdesk-Secret`     |
| 404       | Ticket not found (unknown `external_id`)     |
| 200/201   | Success                                      |
