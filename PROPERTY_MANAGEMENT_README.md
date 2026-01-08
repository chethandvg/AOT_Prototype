# Property Management System

A Blazor WebAssembly frontend with ASP.NET Core Web API backend for managing buildings, units, owners, and ownership shares.

## Projects

### PropertyManagement.Models
Shared domain models used by both the API and Client:
- **Building**: Building entity with code, name, address, property type
- **Unit**: Unit entity with unit number, floor, type, furnishing, status
- **Owner**: Owner entity with contact information
- **BuildingOwnershipShare**: Ownership shares for buildings
- **UnitOwnershipShare**: Ownership shares for units (with override capability)
- **BuildingFile** / **UnitFile**: File attachments for buildings and units

### PropertyManagement.API
ASP.NET Core Web API (.NET 9.0) with the following features:
- SQLite database with Entity Framework Core
- REST API endpoints for CRUD operations
- Swagger/OpenAPI documentation
- CORS configured for Blazor client

#### API Endpoints

**Buildings:**
- `GET /api/buildings` - List buildings with optional search, filters
- `GET /api/buildings/{id}` - Get building details
- `POST /api/buildings` - Create building
- `PUT /api/buildings/{id}` - Update building
- `PUT /api/buildings/{id}/address` - Update building address
- `DELETE /api/buildings/{id}` - Soft delete building

**Units:**
- `GET /api/units` - List units
- `GET /api/units/{id}` - Get unit details
- `POST /api/units` - Create unit
- `POST /api/units/bulk` - Bulk create units
- `PUT /api/units/{id}` - Update unit
- `DELETE /api/units/{id}` - Delete unit

**Owners:**
- `GET /api/owners` - List owners
- `GET /api/owners/{id}` - Get owner details
- `POST /api/owners` - Create owner
- `PUT /api/owners/{id}` - Update owner
- `DELETE /api/owners/{id}` - Delete owner

**Ownership:**
- `GET /api/ownership/buildings/{buildingId}` - Get building ownership shares
- `PUT /api/ownership/buildings/{buildingId}` - Update building ownership shares
- `GET /api/ownership/units/{unitId}` - Get unit ownership shares
- `PUT /api/ownership/units/{unitId}` - Update unit ownership shares
- `DELETE /api/ownership/units/{unitId}/override` - Remove unit ownership override

**Files:**
- `GET /api/files/buildings/{buildingId}` - Get building files
- `POST /api/files/buildings/{buildingId}` - Upload building file
- `DELETE /api/files/buildings/{fileId}` - Soft delete building file
- `GET /api/files/units/{unitId}` - Get unit files
- `POST /api/files/units/{unitId}` - Upload unit file
- `DELETE /api/files/units/{fileId}` - Soft delete unit file

### PropertyManagement.Client
Blazor WebAssembly standalone app (.NET 9.0) with the following features:

#### Pages
- **Home** - Dashboard/landing page
- **Buildings List** - Search, filter, and view all buildings in card layout
- **Building Details** - Tabbed interface with:
  - Units tab - View and manage building units
  - Ownership tab - View building ownership shares
  - Documents tab - View uploaded files
- **Owners List** - Manage owner information with add/edit modal

#### Planned Features
- Create Building Wizard (5-step wizard for building creation)
- Unit add/edit modals
- Bulk unit creation
- Ownership editor component (reusable for buildings and units)
- File upload with drag-and-drop

## Validation Rules

The API enforces the following business rules:

1. **Building Code Uniqueness**: `BuildingCode` must be unique per Organization
2. **Unit Number Uniqueness**: `UnitNumber` must be unique per Building
3. **Ownership Share Validation**:
   - Sum must equal exactly 100.00 (with epsilon tolerance of 0.01)
   - Each owner can appear only once per ownership set
   - Share percent must be greater than 0
4. **Unit Ownership Override**: Units can override building-level ownership

## Running the Application

### Prerequisites
- .NET 9.0 SDK

### Running the API
```bash
cd src/PropertyManagement.API
dotnet run
```
The API will be available at `https://localhost:7001` (HTTPS) or `http://localhost:5001` (HTTP).
Swagger UI will be available at `https://localhost:7001/swagger`.

### Running the Blazor Client
```bash
cd src/PropertyManagement.Client
dotnet run
```
The client will be available at `https://localhost:5002` (HTTPS) or `http://localhost:5000` (HTTP).

**Note**: Update the `ApiBaseAddress` in the client's `appsettings.json` to point to the API URL if different from default.

## Database

The API uses SQLite for storage. The database is automatically created on first run at:
```
src/PropertyManagement.API/propertymanagement.db
```

## Architecture

- **Models Layer**: Shared domain models between API and Client
- **API Layer**: ASP.NET Core Web API with controllers, DbContext
- **Client Layer**: Blazor WebAssembly SPA with pages and components
- **Communication**: REST API over HTTP/HTTPS
- **Data Storage**: SQLite with Entity Framework Core

## Future Enhancements

- Complete the Create Building Wizard
- Add authentication and authorization
- Implement real-time updates with SignalR
- Add unit tests
- Add file download capability
- Implement actual file storage (currently just metadata)
- Add pagination for large lists
- Add data export capabilities
- Implement advanced search and filtering
