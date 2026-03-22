# Christina Ticketing System API (C# / ASP.NET Core)

Beginner-friendly Web API that manages support tickets with a radiant pink & mint UI.

## 1. Run the API + UI

From a terminal:

```bash
cd c:\ChristinaTicketingSystem\ChristinaTicketingSystem.Api
dotnet run
```

You should see a line like:

```text
Now listening on: http://localhost:5097
```

Open the browser at:

- UI: `http://localhost:5097/`
- API root example: `http://localhost:5097/api/tickets`

If things look stuck or cached, press **Ctrl + F5** in the browser.

## 2. Ticket model

Each ticket has:

- `Id` (int)
- `Title` (string)
- `Description` (string)
- `Category` (string)
- `Status` (enum): `Open`, `InProgress`, `Resolved`, `Closed`
- `CreatedDate` (DateTime, UTC)
- `AssignedTo` (string?, optional)

Example JSON from the API:

```json
{
  "id": 1,
  "title": "Login page bug",
  "description": "Error when logging in with valid credentials.",
  "category": "Bug",
  "status": "Open",
  "createdDate": "2026-03-13T07:40:00Z",
  "assignedTo": "Christina"
}
```

## 3. Endpoints

Base URL (when running locally): `http://localhost:5097`

### GET /api/tickets

- **Purpose**: Get all tickets.
- **Request body**: none.
- **Response 200 OK**: array of tickets.

Example with curl:

```bash
curl http://localhost:5097/api/tickets
```

### GET /api/tickets/{id}

- **Purpose**: Get one ticket by id.
- **Request body**: none.
- **Response 200 OK**: ticket JSON.
- **Response 404 Not Found**: if the ticket doesn’t exist.

Example:

```bash
curl http://localhost:5097/api/tickets/1
```

### POST /api/tickets

- **Purpose**: Create a new ticket.
- **Request body**: `TicketCreateDto`

```json
{
  "title": "Login page bug",
  "description": "Error when logging in with valid credentials.",
  "category": "Bug",
  "assignedTo": "Christina"
}
```

- **Responses**:
  - `201 Created` with the created ticket.
  - `400 Bad Request` if validation fails.

### PUT /api/tickets/{id}

- **Purpose**: Replace an existing ticket.
- **Request body**: `TicketUpdateDto`

```json
{
  "title": "Login page bug (updated)",
  "description": "More details here...",
  "category": "Bug",
  "assignedTo": "Christina",
  "status": "InProgress"
}
```

- **Responses**:
  - `204 No Content` on success.
  - `404 Not Found` if the ticket doesn’t exist.

### DELETE /api/tickets/{id}

- **Purpose**: Delete a ticket.
- **Request body**: none.
- **Responses**:
  - `204 No Content` on success.
  - `404 Not Found` if the ticket doesn’t exist.

### PATCH /api/tickets/{id}/status

- **Purpose**: Change only the ticket status.
- **Request body**: `TicketStatusUpdateDto`

```json
{
  "status": "Resolved"
}
```

- **Responses**:
  - `204 No Content` on success.
  - `404 Not Found` if the ticket doesn’t exist.

## 4. Testing with Swagger / Postman

### Swagger / OpenAPI

This project uses the built-in ASP.NET Core OpenAPI support.

- When running in Development:
  - OpenAPI JSON is available at `http://localhost:5097/openapi/v1.json` (or similar).
  - You can import this URL into tools like **Postman** or **Insomnia** as an OpenAPI definition.

### Postman examples

1. **Create a ticket**
   - Method: `POST`
   - URL: `http://localhost:5097/api/tickets`
   - Body: **raw** JSON

   ```json
   {
     "title": "Payment gateway down",
     "description": "Customers cannot pay with card.",
     "category": "Incident",
     "assignedTo": "On-call engineer"
   }
   ```

2. **List all tickets**
   - Method: `GET`
   - URL: `http://localhost:5097/api/tickets`

3. **Update status only**
   - Method: `PATCH`
   - URL: `http://localhost:5097/api/tickets/1/status`
   - Body: **raw** JSON

   ```json
   {
     "status": "Resolved"
   }
   ```

## 5. Where to look in the code

- `Program.cs`  
  Sets up routing, controllers, JSON options (enums as strings), and static file hosting.

- `Models/Ticket.cs`  
  Contains the `Ticket` entity and `TicketStatus` enum.

- `Models/Dtos/TicketDtos.cs`  
  DTOs for create, update, read, and status update.

- `Controllers/TicketsController.cs`  
  All six endpoints:
  - `GET /api/tickets`
  - `GET /api/tickets/{id}`
  - `POST /api/tickets`
  - `PUT /api/tickets/{id}`
  - `DELETE /api/tickets/{id}`
  - `PATCH /api/tickets/{id}/status`
  Uses an in-memory list for storage to keep things simple for learning.

- `wwwroot/index.html`, `wwwroot/css/site.css`, `wwwroot/js/app.js`  
  The radiant pink & mint UI that talks to the API using `fetch`.

## 6. Next steps / practice ideas

- Add filtering: `GET /api/tickets?status=Open`.
- Add simple search by title or category.
- Add basic authentication / API key.
- Replace in-memory storage with a real database (e.g., EF Core + SQLite).

