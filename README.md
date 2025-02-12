# Bus Info

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17.2-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Stack-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)

## Overview

This repository contains the source code for my Bus Info project. It provides almost real-time information about bus arrivals at Runshaw College.

## Technical Framework

### Core Technologies

- ASP.NET Core 9.0
- PostgreSQL 17.2
- Redis Stack
- Prediction system (WIP)

### System Structure

The application utilises a layered approach:

1. Data Storage: PostgreSQL 17.2
2. Cache System: Redis Stack
3. Processing Layer: ASP.NET Core 9.0
4. Interface Layer: Web-based presentation

### Primary Components

#### Data Management

- PostgreSQL 17.2 with index optimisation
- Redis-based caching
- Serilog with PostgreSQL integration

#### Authentication Methods

- Cookie-based web authentication
- API key system for external access
- Role-based access controls

#### Performance Considerations

- Redis cache implementation
- Asynchronous operations
- Request monitoring systems

## Development Configuration

### Requirements

```plaintext
- .NET 9.0 SDK
- PostgreSQL 17.2
- Redis Stack
- Node.js
```

### Installation Steps

1. **Clone and Setup**

   ```shell
   # Clone the repository
   git clone https://github.com/Jacob-Walton/buses-info.git
   cd buses-info
   ```

2. **Configure Settings**

   ```shell
   # Rename the settings file
   $ mv appsettings.example.json appsettings.json
   
   # Edit the settings file with your values
   $ nano appsettings.json
   ```

3. **Install Dependencies**

   ```shell
   # Restore .NET packages
   $ dotnet restore
   ```

4. **Database Setup**

   ```shell
   # Create the database
   $ dotnet ef database update
   ```

5. **Static Files**

   ```shell
   # Move to watcher directory
   $ cd watcher

   # Install dependencies
   $ npm install

   # Build the files
   $ npm run build
   ```

6. **Run the Application**

   ```shell
   # Move back to the root directory
   $ cd ..

   # Start the application
   $ dotnet run --environment Development
   
   # The application will be available at:
   # http://localhost:{port}
   ```

> **Note:** Ensure you have configured your keyvault and filled in all required values in appsettings.json before starting the application.

## API Documentation

The system provides a REST API for external access. The API is versioned to allow for future updates.

### V1 API

```http
GET /api/v1/businfo
Headers:
    X-Api-Key: {key}
```

Response format:

```json
{
  "busData": {
    "102": "B1"
  },
  "lastUpdated": "2025-02-03T12:00:00Z"
}
```

### V2 API

```http
GET /api/v2/businfo
Headers:
    X-Api-Key: {key}
```

Response format:

```json
{
  "busData": {
    "102": {
      "status": "Arrived",
      "bay": "A1"
    }
  },
  "lastUpdated": "2025-02-03T12:00:00Z",
  "status": "OK"
}
```

```http
GET /api/v2/businfo/predictions
Headers:
    X-Api-Key: {key}
```

Response format:

```json
{
  "predictions": {
    "102": {
      "predictions": [
        {
          "bay": "A14",
          "probability": 100
        }
      ],
      "overallConfidence": 100
    },
    "103": {
      "predictions": [
        {
          "bay": "A9",
          "probability": 100
        }
      ],
      "overallConfidence": 100
    },
    "115": {
      "predictions": [
        {
          "bay": "C13",
          "probability": 100
        }
      ],
      "overallConfidence": 100
    }
  }
}
```

```http
GET /api/v2/businfo/predictions/809;819

Headers:
    X-Api-Key: {key}
```

Response format:

```json
{
  "809": {
    "predictions": [
      {
        "bay": "B2",
        "probability": 100
      }
    ],
    "overallConfidence": 100
  },
  "819": {
    "predictions": [
      {
        "bay": "B3",
        "probability": 100
      }
    ],
    "overallConfidence": 100
  }
}
```

## Security Measures

The system implements standard security protocols:

- HTTPS enforcement
- CSRF protection
- Rate limiting
- Cookie security
- Input validation
- XSS mitigation

## Development Status

This project is under active development. Contributions must follow established coding standards.

## Legal Statement

This project is an independent project and is not affiliated with any external organisations.

## License

This project is licensed under the MIT licence. For more information, see the [LICENCE](LICENSE) file.

## Contact

For any enquiries:

- Email: [jacob-walton@konpeki.co.uk](mailto:jacob-walton@konpeki.co.uk)
