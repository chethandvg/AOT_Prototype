# Property Management System - Quick Start Guide

## Prerequisites
- .NET 9.0 SDK installed
- Git (for cloning the repository)

## Running the Application

### Step 1: Start the API Server

Open a terminal and run:

```bash
cd src/PropertyManagement.API
dotnet run
```

The API will start on:
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:5001`

**Swagger UI** (API documentation and testing):
- Navigate to: `https://localhost:7001/swagger`

### Step 2: Start the Blazor Client

Open a **new terminal** (keep the API running) and run:

```bash
cd src/PropertyManagement.Client
dotnet run
```

The client will start on:
- HTTPS: `https://localhost:5002`
- HTTP: `http://localhost:5000`

Open your browser and navigate to: `http://localhost:5000`

## Testing the Application

### Using the Blazor UI

1. **Navigate to Buildings**:
   - Click "Buildings" in the navigation menu
   - You'll see an empty list initially
   - Click "Create Building" to add a new building

2. **Navigate to Owners**:
   - Click "Owners" in the navigation menu
   - Click "Add Owner" to create an owner
   - Fill in the form and save

### Using Swagger UI (API Testing)

Navigate to `https://localhost:7001/swagger` and try these operations:

#### Create an Owner
1. Expand `POST /api/owners`
2. Click "Try it out"
3. Use this JSON:
```json
{
  "name": "John Doe",
  "email": "john.doe@example.com",
  "phone": "+1-555-0100",
  "organizationId": "ORG001"
}
```
4. Click "Execute"
5. Note the `id` in the response

#### Create a Building
1. Expand `POST /api/buildings`
2. Click "Try it out"
3. Use this JSON:
```json
{
  "code": "BLD001",
  "name": "Sunset Apartments",
  "organizationId": "ORG001",
  "propertyType": 0,
  "address": {
    "street": "123 Main St",
    "city": "Springfield",
    "state": "IL",
    "postalCode": "62701",
    "country": "USA"
  }
}
```
4. Click "Execute"
5. Note the `id` in the response

#### Add Units to Building
1. Expand `POST /api/units/bulk`
2. Use this JSON (replace buildingId with the ID from above):
```json
{
  "buildingId": 1,
  "prefix": "A-",
  "start": 101,
  "end": 105,
  "floor": 1,
  "type": 1,
  "furnishing": 0
}
```
3. This will create units: A-101, A-102, A-103, A-104, A-105

#### Set Building Ownership
1. Expand `PUT /api/ownership/buildings/{buildingId}`
2. Replace `{buildingId}` with your building ID
3. Use this JSON (replace ownerId with the owner ID from step 1):
```json
[
  {
    "ownerId": 1,
    "sharePercent": 100.00,
    "effectiveDate": "2026-01-08"
  }
]
```

#### Verify via UI
1. Go back to `http://localhost:5000`
2. Click "Buildings" - you should see your building
3. Click on the building card to view details
4. See the units in the Units tab
5. See the ownership in the Ownership tab

## Sample Test Scenarios

### Scenario 1: Create a Multi-Owner Building

1. Create two owners via API or UI
2. Create a building
3. Set ownership with two owners:
```json
[
  {
    "ownerId": 1,
    "sharePercent": 60.00
  },
  {
    "ownerId": 2,
    "sharePercent": 40.00
  }
]
```

### Scenario 2: Unit Ownership Override

1. Create a building with ownership
2. Add units to the building
3. Override ownership for a specific unit:
```json
PUT /api/ownership/units/{unitId}
[
  {
    "ownerId": 2,
    "sharePercent": 100.00
  }
]
```

### Scenario 3: Test Validation Rules

Try these to verify validation works:

**Duplicate Building Code:**
```bash
POST /api/buildings (with same code and organizationId)
# Expected: 400 Bad Request with error message
```

**Invalid Ownership (not 100%):**
```bash
PUT /api/ownership/buildings/{id}
[{"ownerId": 1, "sharePercent": 50.00}]
# Expected: 400 Bad Request - shares must sum to 100
```

**Duplicate Owner in Shares:**
```bash
PUT /api/ownership/buildings/{id}
[
  {"ownerId": 1, "sharePercent": 50.00},
  {"ownerId": 1, "sharePercent": 50.00}
]
# Expected: 400 Bad Request - duplicate owner
```

## Database Location

The SQLite database is created at:
```
src/PropertyManagement.API/propertymanagement.db
```

To reset the database, simply delete this file and restart the API.

## Troubleshooting

### API won't start
- Check if port 7001 (HTTPS) or 5001 (HTTP) is already in use
- Try changing ports in `launchSettings.json`

### Client can't connect to API
- Verify API is running
- Check the API base address in client configuration
- Ensure CORS is configured correctly

### Database errors
- Delete the `.db` file and restart
- Check file permissions in the API directory

## Development Tips

### Hot Reload
Both projects support hot reload:
- Changes to Razor files reload automatically
- Changes to C# code require app restart

### Debugging
- Set breakpoints in Visual Studio or VS Code
- Use browser developer tools for client-side debugging
- Check console for error messages

### Adding Test Data
Use Swagger UI to quickly add test data through the API endpoints.

## Next Steps

After basic testing:
1. Review the code in `src/PropertyManagement.API/Controllers/`
2. Explore the Blazor pages in `src/PropertyManagement.Client/Pages/`
3. Check the models in `src/PropertyManagement.Models/`
4. Read the full documentation in `PROPERTY_MANAGEMENT_README.md`

## API Endpoints Reference

Quick reference for all available endpoints:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/buildings | List all buildings |
| POST | /api/buildings | Create building |
| GET | /api/buildings/{id} | Get building details |
| PUT | /api/buildings/{id} | Update building |
| DELETE | /api/buildings/{id} | Delete building |
| GET | /api/units | List all units |
| POST | /api/units | Create unit |
| POST | /api/units/bulk | Bulk create units |
| GET | /api/units/{id} | Get unit details |
| PUT | /api/units/{id} | Update unit |
| DELETE | /api/units/{id} | Delete unit |
| GET | /api/owners | List all owners |
| POST | /api/owners | Create owner |
| GET | /api/owners/{id} | Get owner details |
| PUT | /api/owners/{id} | Update owner |
| DELETE | /api/owners/{id} | Delete owner |
| GET | /api/ownership/buildings/{id} | Get building ownership |
| PUT | /api/ownership/buildings/{id} | Update building ownership |
| GET | /api/ownership/units/{id} | Get unit ownership |
| PUT | /api/ownership/units/{id} | Update unit ownership |
| DELETE | /api/ownership/units/{id}/override | Remove override |
| GET | /api/files/buildings/{id} | Get building files |
| POST | /api/files/buildings/{id} | Upload building file |
| DELETE | /api/files/buildings/{fileId} | Delete building file |
| GET | /api/files/units/{id} | Get unit files |
| POST | /api/files/units/{id} | Upload unit file |
| DELETE | /api/files/units/{fileId} | Delete unit file |

For detailed API documentation, visit the Swagger UI when the API is running.
