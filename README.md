# Buses Info

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17.2-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Stack-DC382D?style=for-the-badge&logo=redis&logoColor=white)

## Overview

A real-time bus arrival information system for Runshaw College.

## Technical Architecture

### Core Technology Stack

- **Framework**: ASP.NET Core 10.0
- **Database**: PostgreSQL 17.2
- **Cache**: Redis Stack
- **ML Component**: Prediction system for arrival forecasting (not yet implemented)

### System Components

#### Data Layer

- PostgreSQL with optimized indexing for transport data
- Redis for high-performance caching
- Serilog with PostgreSQL sink for structured logging

#### Authentication System

- Cookie-based authentication for web interface
- API key authorization for external integrations
- Role-based access control

#### Performance Optimizations

- Redis caching implementation
- Asynchronous processing patterns
- Request monitoring and metrics collection

## Installation

### Prerequisites

- .NET 10.0 SDK
- PostgreSQL 17.2
- Redis Stack
- Node.js
- OpenTofu 1.9.0+
- AWS CLI configured with appropriate credentials

### Setup Process

1. **Repository Setup**

   ```bash
   git clone https://github.com/Jacob-Walton/buses-info.git
   cd buses-info
   ```

2. **Configuration**

   ```bash
   cp appsettings.example.json appsettings.json
   # Edit configuration with appropriate values
   ```

3. **Backend Setup**

   ```bash
   dotnet restore
   dotnet ef database update
   ```

4. **Frontend Build**

   ```bash
   cd watcher
   npm install
   npm run build
   cd ..
   ```

5. **Application Execution**

   ```bash
   dotnet run --environment Development
   ```

> [!NOTE]
> Ensure all connection strings and required values are
> properly configured in `appsettings.json` before starting
> the application.

## Infrastructure

The application stores and retrieves content from AWS infstructure provisioned and managed by OpenTofu.

### Cloud Resources

- **S3 Bucket**: Hosts static assets in Frankfurt (eu-central-1)
- **CloudFront Distribution**: Global CDN for static assets
- **Origin Access Control**: Secures S3 bucket access

### Infrastructure Deployment

1. **Navigate to Infrastructure Dir**

   ```bash
   cd infrastructure
   ```

2. **Initialize OpenTofu**

   ```bash
   tofu init
   ```

3. **Deploy Infrastructure**

   ```bash
   tofu apply
   ```

4. **Access Outputs**

The deployment provides the S3 bucket name and CloudFront distribution domain.

> [!IMPORTANT]
> The OpenTofu configuration creates resources optimized for AWS free tier.
> Ensure your AWS credentials are configured with appropriate permissions.

## Infrastructure Management

- Check Deployment Status

  ```bash
  tofu status
  ```

- Update Infrastructure

  ```bash
  tofu apply
  ```

- Remove Infrastructure

  ```bash
  tofu destroy
  ```

## API Reference

The system exposes REST APIs for integration with external systems.

### V1 API (Legacy)

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

### V2 API (Current)

#### General Bus Information

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

#### Prediction Endpoints

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
    }
  }
}
```

```http
GET /api/v2/businfo/predictions/{bus-numbers}
Headers:
    X-Api-Key: {key}
```

Example:

```http
GET /api/v2/businfo/predictions/809;819
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

## Security Implementation

The application implements industry-standard security measures:

- HTTPS enforcement
- CSRF protection
- API rate limiting
- Secure cookie configuration
- Input validation and sanitization
- XSS mitigation techniques

## Development Status

This project is under active development. Contributions must follow the project coding standards and undergo review before acceptance.

## Disclaimer

This is an independent project and is not affiliated with or endorsed by any public transportation authority or Runshaw College.

## License

This project is licensed under the License for Buses-Info. See the [LICENSE](LICENSE) file for details.

## Contact

For inquiries or contributions:

- Email: [jacob-walton@konpeki.co.uk](mailto:jacob-walton@konpeki.co.uk)
- Issues: GitHub issue tracker
