param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$CustomerEmail = "customer@commerce.local",
    [string]$CustomerPassword = "Customer123!",
    [string]$AdminEmail = "admin@commerce.local",
    [string]$AdminPassword = "Administrator123!"
)

$ErrorActionPreference = "Stop"

function Wait-UntilReady {
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        try {
            Invoke-RestMethod -Uri "$BaseUrl/health/ready" -TimeoutSec 3 | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Gateway readiness timed out."
}

function Login([string]$Email, [string]$Password) {
    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    return Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType "application/json" -Body $body
}

Wait-UntilReady
$customer = Login $CustomerEmail $CustomerPassword
$admin = Login $AdminEmail $AdminPassword
$customerHeaders = @{ Authorization = "Bearer $($customer.accessToken)" }
$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }

$firstCatalog = Invoke-WebRequest -Uri "$BaseUrl/api/catalog/products"
$secondCatalog = Invoke-WebRequest -Uri "$BaseUrl/api/catalog/products"
$products = $firstCatalog.Content | ConvertFrom-Json
$product = $products.items | Where-Object sku -eq "MACBOOK-PRO-001" | Select-Object -First 1
if ($null -eq $product) { throw "Seeded MacBook product was not returned." }

$cartBody = @{ quantity = 1 } | ConvertTo-Json
$cart = Invoke-RestMethod -Method Put -Uri "$BaseUrl/api/cart/items/$($product.id)" `
    -Headers $customerHeaders -ContentType "application/json" -Body $cartBody
if ($cart.items.Count -lt 1) { throw "Cart did not retain the seeded product." }

$checkoutBody = @{
    recipientName = "Commerce Customer"
    addressLine1 = "1 Reference Architecture Way"
    city = "Hanoi"
    postalCode = "100000"
    countryCode = "VN"
} | ConvertTo-Json
$checkoutHeaders = $customerHeaders.Clone()
$checkoutHeaders["Idempotency-Key"] = "commerce-smoke-checkout-v1"
$checkout = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/orders/checkout" `
    -Headers $checkoutHeaders -ContentType "application/json" -Body $checkoutBody
$checkoutReplay = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/orders/checkout" `
    -Headers $checkoutHeaders -ContentType "application/json" -Body $checkoutBody
if ($checkoutReplay.orderId -ne $checkout.orderId) {
    throw "Checkout idempotency returned a different order for the same key."
}

$order = $null
for ($attempt = 1; $attempt -le 60; $attempt++) {
    $order = Invoke-RestMethod -Uri "$BaseUrl/api/orders/$($checkout.orderId)" -Headers $customerHeaders
    if ($order.status -in @("Shipped", "Delivered")) { break }
    Start-Sleep -Seconds 2
}
if ($order.status -notin @("Shipped", "Delivered")) {
    throw "Order did not complete shipment; last state was $($order.status)."
}

$payment = Invoke-RestMethod -Uri "$BaseUrl/api/payments/orders/$($checkout.orderId)" -Headers $adminHeaders
if ($payment.status -ne "Succeeded") { throw "Payment state was $($payment.status)." }

$shipment = Invoke-RestMethod -Uri "$BaseUrl/api/shipping/orders/$($checkout.orderId)" -Headers $customerHeaders
$inventory = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/$($product.id)" -Headers $adminHeaders
if ($inventory.reservedQuantity -ne 0) { throw "Inventory reservation was not confirmed." }

Write-Host "Smoke test passed."
Write-Host "Catalog instances: $($firstCatalog.Headers['X-Service-Instance']) -> $($secondCatalog.Headers['X-Service-Instance'])"
Write-Host "Order: $($order.id) status=$($order.status)"
Write-Host "Payment: $($payment.status); shipment=$($shipment.status); stock=$($inventory.quantityOnHand)"
