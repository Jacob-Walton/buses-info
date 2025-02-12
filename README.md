# Bus Info

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17.2-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Stack-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)

## Overview

This repository documents the development of BusInfo, a transit information system developed to analyse and present real-time transport data.

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

- PostgreSQL 16.2 with index optimisation
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
- PostgreSQL 16.2
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

4. **Run the Application**

   ```shell
   # Start the application
   $ dotnet run
   
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
  "lastUpdated": "2025-02-03T12:00:00Z"
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
          "bay": "A13",
          "probability": 52
        },
        {
          "bay": "C13",
          "probability": 37
        },
        {
          "bay": "B8",
          "probability": 37
        }
      ]
    }
  },
  "lastUpdated": "2025-02-03T12:00:00Z"
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
  "predictions": {
    "809": {
      "predictions": [
        {
          "bay": "A13",
          "probability": 52
        },
        {
          "bay": "C13",
          "probability": 37
        },
        {
          "bay": "B8",
          "probability": 37
        }
      ]
    },
    "819": {
      "predictions": [
        {
          "bay": "A13",
          "probability": 52
        },
        {
          "bay": "C13",
          "probability": 37
        },
        {
          "bay": "B8",
          "probability": 37
        }
      ]
    }
  },
  "lastUpdated": "2025-02-03T12:00:00Z"
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

This project is an independent research initiative with no affiliation to Runshaw College.

## Contact

For technical enquiries:

- Email: [jacob-walton@konpeki.co.uk](mailto:jacob-walton@konpeki.co.uk)
