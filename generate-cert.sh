#!/usr/bin/env bash
# Polyglot script for certificate generation
# Works in Bash and PowerShell
# For Bash: ./generate-cert.sh
# For PowerShell: powershell -ExecutionPolicy Bypass -File generate-cert.sh

# Determine if running in PowerShell or Bash
if [ -z "$PWSHDETECT" ]; then
  # This is the Bash implementation
  bash_impl() {
    echo -e "\033[0;32mGenerating self-signed certificate for HTTPS using Bash...\033[0m"

    # Function to check if a command exists
    command_exists() {
        command -v "$1" >/dev/null 2>&1
    }

    # Check for required tools
    if ! command_exists openssl; then
        echo -e "\033[0;31mError: OpenSSL is required but not installed.\033[0m"
        echo "Please install OpenSSL and try again."
        echo "  - On Ubuntu/Debian: sudo apt-get install openssl"
        echo "  - On macOS with Homebrew: brew install openssl"
        exit 1
    fi

    # Create certs directory if it doesn't exist
    mkdir -p certs
    echo -e "\033[0;90mCreated certs directory\033[0m"

    # Prompt for password
    read -p "Enter password for certificate: " CERT_PASSWORD

    # Generate a self-signed certificate
    echo -e "\033[0;90mGenerating certificate...\033[0m"
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
      -keyout certs/server.key -out certs/server.crt \
      -subj "/CN=localhost" \
      -addext "subjectAltName=DNS:localhost,DNS:bus-info,IP:127.0.0.1" 2>/dev/null

    if [ $? -ne 0 ]; then
        # Try older OpenSSL version syntax
        openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
          -keyout certs/server.key -out certs/server.crt \
          -subj "/CN=localhost" 2>/dev/null
        # Add SAN manually for older OpenSSL
        echo "subjectAltName=DNS:localhost,DNS:bus-info,IP:127.0.0.1" > certs/san.ext
        openssl x509 -in certs/server.crt -out certs/server.crt -extfile certs/san.ext 2>/dev/null
        rm -f certs/san.ext
    fi

    # Create pfx file for .NET
    openssl pkcs12 -export -out certs/server.pfx -inkey certs/server.key -in certs/server.crt -passout pass:"$CERT_PASSWORD"
    echo -e "\033[0;90mCertificates exported to $(pwd)/certs/server.pfx and $(pwd)/certs/server.crt\033[0m"

    # Create .env file with the password - this file should be in .gitignore
    echo "# Certificate password for HTTPS" > .env
    echo "CERT_PASSWORD=$CERT_PASSWORD" >> .env
    echo "# Add this file to .gitignore to avoid committing sensitive data" >> .env
    echo -e "\033[0;90mCreated .env file with certificate password\033[0m"

    # Ensure .env is in .gitignore
    if [ -f ".gitignore" ]; then
        if ! grep -q "^\.env$" .gitignore; then
            echo ".env" >> .gitignore
            echo -e "\033[0;90mAdded .env to .gitignore\033[0m"
        fi
    else
        echo ".env" > .gitignore
        echo -e "\033[0;90mCreated .gitignore with .env entry\033[0m"
    fi

    # Set appropriate permissions
    chmod 644 certs/server.crt certs/server.key certs/server.pfx
    
    echo -e "\033[0;33mCertificate password is stored in .env file (not in version control)\033[0m"
    echo -e "\033[0;33mIn docker-compose, use \${CERT_PASSWORD} to access it\033[0m"

    # Platform-specific certificate trust
    if [ "$(uname)" == "Darwin" ]; then
        echo -e "\033[0;33mOn macOS, you may want to trust this certificate.\033[0m"
        read -p "Add certificate to macOS keychain? (y/n) " ADD_TO_KEYCHAIN
        if [ "$ADD_TO_KEYCHAIN" == "y" ] || [ "$ADD_TO_KEYCHAIN" == "Y" ]; then
            sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/server.crt
            echo -e "\033[0;32mCertificate added to keychain\033[0m"
        fi
    elif [ "$(uname)" == "Linux" ]; then
        echo -e "\033[0;33mOn Linux, you may need to trust this certificate manually.\033[0m"
        echo "This varies by distribution, but you may try:"
        echo "  - Ubuntu/Debian: sudo cp certs/server.crt /usr/local/share/ca-certificates/ && sudo update-ca-certificates"
        echo "  - CentOS/RHEL: sudo cp certs/server.crt /etc/pki/ca-trust/source/anchors/ && sudo update-ca-trust"
    fi

    echo -e "\033[0;32mHTTPS configuration complete!\033[0m"
  }

  # Execute the Bash implementation and exit
  bash_impl
  exit 0
fi

# PowerShell implementation
try {
    Write-Host "Generating self-signed certificate for HTTPS using PowerShell..." -ForegroundColor Green

    # Create certs directory if it doesn't exist
    if (-not (Test-Path -Path "certs")) {
        New-Item -ItemType Directory -Path "certs" -Force | Out-Null
        Write-Host "Created certs directory" -ForegroundColor Gray
    }

    # Prompt for password
    $securePassword = Read-Host -Prompt "Enter password for certificate" -AsSecureString
    $password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))

    # Generate self-signed certificate
    Write-Host "Generating certificate..." -ForegroundColor Gray
    $cert = New-SelfSignedCertificate -DnsName "localhost", "bus-info" -CertStoreLocation "cert:\LocalMachine\My"
    
    # Export the certificate as PFX
    $certPath = "cert:\LocalMachine\My\$($cert.Thumbprint)"
    $pfxPath = Join-Path -Path (Get-Location) -ChildPath "certs\server.pfx"
    $cerPath = Join-Path -Path (Get-Location) -ChildPath "certs\server.crt"

    Export-PfxCertificate -Cert $certPath -FilePath $pfxPath -Password (ConvertTo-SecureString -String $password -Force -AsPlainText)
    Export-Certificate -Cert $certPath -FilePath $cerPath -Type CERT
    Write-Host "Certificates exported to $pfxPath and $cerPath" -ForegroundColor Gray

    # Create .env file with password
    $envContent = @"
# Certificate password for HTTPS
CERT_PASSWORD=$password
# Add this file to .gitignore to avoid committing sensitive data
"@
    Set-Content -Path ".env" -Value $envContent
    Write-Host "Created .env file with certificate password" -ForegroundColor Gray

    # Ensure .env is in .gitignore
    if (Test-Path -Path ".gitignore") {
        $gitignoreContent = Get-Content -Path ".gitignore" -Raw
        if ($gitignoreContent -notmatch "(?m)^\.env$") {
            Add-Content -Path ".gitignore" -Value ".env"
            Write-Host "Added .env to .gitignore" -ForegroundColor Gray
        }
    } else {
        Set-Content -Path ".gitignore" -Value ".env"
        Write-Host "Created .gitignore with .env entry" -ForegroundColor Gray
    }

    Write-Host "Certificate password is stored in .env file (not in version control)" -ForegroundColor Yellow
    Write-Host "In docker-compose, use `${CERT_PASSWORD} to access it" -ForegroundColor Yellow
    
    Write-Host "Certificate generated successfully!" -ForegroundColor Green
    Write-Host "To trust this certificate, run:" -ForegroundColor Yellow
    Write-Host "Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation cert:\LocalMachine\Root" -ForegroundColor Yellow
} catch {
    Write-Host "An error occurred: $_" -ForegroundColor Red
    exit 1
}
