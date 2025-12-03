# Database Schema Change Detection Script
# Checks if database models were changed without updating schema version

param(
    [string]$ProjectPath = ".",
    [switch]$Fix = $false
)

$ModelsPath = Join-Path $ProjectPath "Models"
$ServicePath = Join-Path $ProjectPath "Services\DatabaseMigrationService.cs"

Write-Host "Database Change Detection" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Get current schema version
$migrationContent = Get-Content $ServicePath -Raw
if ($migrationContent -match 'CURRENT_SCHEMA_VERSION\s*=\s*(\d+)') {
    $currentVersion = [int]$matches[1]
    Write-Host "Current schema version: $currentVersion" -ForegroundColor Green
} else {
    Write-Host "Cannot find CURRENT_SCHEMA_VERSION!" -ForegroundColor Red
    exit 1
}

# Check for new ObservableProperty in models
$modelFiles = Get-ChildItem $ModelsPath -Filter "*.cs" -Recurse
$newProperties = @()

foreach ($file in $modelFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Find all ObservableProperty declarations  
    $properties = [regex]::Matches($content, '\[ObservableProperty\]\s*\n\s*private\s+[\w\?<>,\s]+\s+(\w+)\s*[=;]')
    
    foreach ($prop in $properties) {
        $propertyName = $prop.Groups[1].Value
        
        # Check if this property has corresponding migration or is part of initial schema
        $hasDiscountMigration = $migrationContent -match "ALTER TABLE.*ADD COLUMN.*$propertyName"
        $hasCreateTableMigration = $migrationContent -match "CREATE TABLE.*$($file.BaseName).*$propertyName"
        $hasMigration = $hasDiscountMigration -or $hasCreateTableMigration
        
        # All known properties from initial schema (created by EnsureCreated)
        $initialProperties = @(
            # Product
            "ean", "name", "category", "salePrice", "purchasePrice", "stockQuantity", "vatRate",
            "discountPercent", "discountValidFrom", "discountValidTo", "discountReason",
            # Receipt
            "receiptId", "receiptYear", "receiptSequence", "paymentMethod", "shopName", "shopAddress", 
            "sellerName", "companyId", "vatId", "isVatPayer", "receivedAmount", "changeAmount", 
            "isStorno", "originalReceiptId", "containsGiftCardSale", "giftCardSaleAmount", "items",
            # ReceiptItem
            "receiptItemId", "receiptId", "productEan", "productName", "quantity",
            "discountPercent", "originalUnitPrice", "discountReason",
            # Return
            "returnId", "originalReceiptId", "shopName", "shopAddress", "companyId", "vatId", "isVatPayer", "items",
            # ReturnItem  
            "returnItemId", "returnId", "productEan", "productName", "returnedQuantity", "priceWithoutVat", "vatAmount",
            # CashRegisterEntry
            "timestamp", "type", "amount", "description", "currentCashInTill",
            # GiftCard
            "value", "issuedDate", "issuedOnReceiptId", "usedDate", "usedOnReceiptId", 
            "expirationDate", "isCancelled", "cancelledDate", "status", "notes", 
            "issuedByUser", "usedByUser", "cancelReason",
            # User
            "userId", "username", "displayName", "password", "role", "isActive", "createdDate",
            # StockMovement
            "movementType", "quantityChange", "stockBefore", "stockAfter", "referenceId", "notes",
            # DailyClose (V10 migration)
            "date", "cashSales", "cardSales", "totalSales", "vatAmount", "receiptNumberFrom", "receiptNumberTo", "closedAt",
            # Receipt payment breakdown (V10 migration)
            "cashAmount", "cardAmount"
        )
        
        $hasInitialSchema = $propertyName -in $initialProperties

        if (-not $hasMigration -and -not $hasInitialSchema) {
            $newProperties += [PSCustomObject]@{
                File = $file.Name
                Property = $propertyName
            }
        }
    }
}

# Report findings
if ($newProperties.Count -gt 0) {
    Write-Host ""
    Write-Host "POTENTIAL DATABASE CHANGES DETECTED!" -ForegroundColor Red
    Write-Host "=====================================" -ForegroundColor Red
    Write-Host ""
    
    foreach ($prop in $newProperties) {
        Write-Host "File: $($prop.File), Property: $($prop.Property)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "These properties might need database migration!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Review the properties above" -ForegroundColor White
    Write-Host "2. If they are new database fields, ask Claude to:" -ForegroundColor White
    Write-Host "   - Increment CURRENT_SCHEMA_VERSION" -ForegroundColor White
    Write-Host "   - Add migration for these fields" -ForegroundColor White
    Write-Host "3. If they are computed properties, ignore this warning" -ForegroundColor White
    
    # Return error code to fail build
    exit 2
} else {
    Write-Host ""
    Write-Host "No new database properties detected" -ForegroundColor Green
    Write-Host "Schema version appears up to date" -ForegroundColor Green
    exit 0
}