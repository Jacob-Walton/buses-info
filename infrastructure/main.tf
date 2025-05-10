terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
  required_version = ">= 1.5.0"
}

# Provider configuration for Frankfurt region
provider "aws" {
  region = "eu-central-1" # Frankfurt
}

# CloudFront needs ACM certificates in us-east-1 region
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}

# Variables
variable "bucket_prefix" {
  description = "Prefix for the S3 bucket name"
  type        = string
  default     = "businfo-content"
}

variable "environment" {
  description = "Environment (e.g., dev, staging, prod)"
  type        = string
  default     = "dev"
}

# Random string for unique bucket name
resource "random_string" "bucket_suffix" {
  length           = 8
  special          = false
  upper            = false
  lower            = true
  numeric          = true
  
  keepers = {
    # Generate a new string only if the environment changes
    environment = var.environment
  }
}

# Create the full bucket name using local value
locals {
  bucket_name = "${var.bucket_prefix}-${random_string.bucket_suffix.result}"
}

# S3 bucket for content
resource "aws_s3_bucket" "content_bucket" {
  bucket   = local.bucket_name
  provider = aws # Frankfurt region
  
  tags = {
    Name        = local.bucket_name
    Environment = var.environment
  }
}

# Enable default server-side encryption
resource "aws_s3_bucket_server_side_encryption_configuration" "content_bucket_encryption" {
  bucket   = aws_s3_bucket.content_bucket.id
  provider = aws
  
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

# Block public access
resource "aws_s3_bucket_public_access_block" "content_bucket_public_access_block" {
  bucket   = aws_s3_bucket.content_bucket.id
  provider = aws
  
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Create Origin Access Control for CloudFront
resource "aws_cloudfront_origin_access_control" "oac" {
  name                              = "${local.bucket_name}-oac"
  description                       = "Origin Access Control for ${local.bucket_name}"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
  provider                          = aws.us_east_1 # CloudFront resources must be created in us-east-1
}

# Create CloudFront distribution
resource "aws_cloudfront_distribution" "cf_distribution" {
  enabled             = true
  is_ipv6_enabled     = true
  default_root_object = "index.html"
  price_class         = "PriceClass_100"
  http_version        = "http2"
  provider            = aws.us_east_1
  
  # Origin configuration
  origin {
    domain_name              = aws_s3_bucket.content_bucket.bucket_regional_domain_name
    origin_id                = "S3-${local.bucket_name}"
    origin_access_control_id = aws_cloudfront_origin_access_control.oac.id
  }

  # Default cache behavior
  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = "S3-${local.bucket_name}"
    
    viewer_protocol_policy = "redirect-to-https"
    compress               = true # Helps reduce data transfer
    
    # Optimize caching to reduce origin requests
    min_ttl                = 86400    # 1 day
    default_ttl            = 86400    # 1 day
    max_ttl                = 31536000 # 1 year

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }
  }

  # Geographical restrictions - none
  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  # SSL/TLS configuration - using CloudFront default certificate
  viewer_certificate {
    cloudfront_default_certificate = true
  }

  tags = {
    Name        = "${local.bucket_name}-cf-distribution"
    Environment = var.environment
  }
}

# Create S3 bucket policy to allow CloudFront access
resource "aws_s3_bucket_policy" "bucket_policy" {
  bucket   = aws_s3_bucket.content_bucket.id
  provider = aws
  
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowCloudFrontServicePrincipal"
        Effect    = "Allow"
        Principal = { Service = "cloudfront.amazonaws.com" }
        Action    = "s3:GetObject"
        Resource  = "${aws_s3_bucket.content_bucket.arn}/*"
        Condition = {
          StringEquals = {
            "AWS:SourceArn" = aws_cloudfront_distribution.cf_distribution.arn
          }
        }
      }
    ]
  })

  depends_on = [aws_s3_bucket_public_access_block.content_bucket_public_access_block]
}

# Outputs
output "s3_bucket_name" {
  description = "Name of the S3 bucket"
  value       = aws_s3_bucket.content_bucket.id
}

output "cloudfront_distribution_domain_name" {
  description = "Domain name of the CloudFront distribution"
  value       = aws_cloudfront_distribution.cf_distribution.domain_name
}