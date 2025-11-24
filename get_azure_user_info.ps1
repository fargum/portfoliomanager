# PowerShell script to get your Azure AD user information
# Install the Microsoft Graph PowerShell module if not already installed:
# Install-Module Microsoft.Graph -Scope CurrentUser -AllowClobber -Force

# Connect to Microsoft Graph (this will prompt for login)
Connect-MgGraph -Scopes "User.Read"

# Get current user information
$currentUser = Get-MgUser -UserId (Get-MgContext).Account

Write-Host "=== Your Azure AD Information ===" -ForegroundColor Green
Write-Host "External User ID (Object ID): " -NoNewline
Write-Host $currentUser.Id -ForegroundColor Yellow
Write-Host "Email: " -NoNewline  
Write-Host $currentUser.Mail -ForegroundColor Yellow
Write-Host "Display Name: " -NoNewline
Write-Host $currentUser.DisplayName -ForegroundColor Yellow
Write-Host ""

Write-Host "=== SQL Update Statement ===" -ForegroundColor Green
Write-Host "Copy and paste this into your database:" -ForegroundColor Cyan
Write-Host ""
Write-Host "UPDATE app.accounts " -ForegroundColor White
Write-Host "SET " -ForegroundColor White
Write-Host "    external_user_id = '$($currentUser.Id)'," -ForegroundColor White  
Write-Host "    email = '$($currentUser.Mail)'," -ForegroundColor White
Write-Host "    display_name = '$($currentUser.DisplayName)'," -ForegroundColor White
Write-Host "    is_active = true," -ForegroundColor White
Write-Host "    last_login_at = NOW()," -ForegroundColor White
Write-Host "    updated_at = NOW()" -ForegroundColor White
Write-Host "WHERE id = 1;" -ForegroundColor White

# Disconnect
Disconnect-MgGraph