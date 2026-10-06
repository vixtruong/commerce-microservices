param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$CustomerPassword = "Customer123!",
    [string]$AdminPassword = "Administrator123!"
)

$ErrorActionPreference = "Stop"

function Login([string]$Email, [string]$Password) {
    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    return Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType "application/json" -Body $body
}

$customer = Login "customer@commerce.local" $CustomerPassword
$admin = Login "admin@commerce.local" $AdminPassword
$customerHeaders = @{ Authorization = "Bearer $($customer.accessToken)" }
$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }
$productId = "22222222-2222-2222-2222-222222222222"
$before = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/$productId" -Headers $adminHeaders

Invoke-RestMethod -Method Put -Uri "$BaseUrl/api/cart/items/$productId" -Headers $customerHeaders `
    -ContentType "application/json" -Body '{"quantity":1}' | Out-Null
$headers = $customerHeaders.Clone()
$headers["Idempotency-Key"] = "commerce-payment-failure-$([Guid]::NewGuid())"
$body = '{"recipientName":"Commerce Customer","addressLine1":"1 Compensation Way","city":"Hanoi","postalCode":"100000","countryCode":"VN"}'
$checkout = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/orders/checkout" -Headers $headers `
    -ContentType "application/json" -Body $body

$order = $null
for ($attempt = 1; $attempt -le 60; $attempt++) {
    $order = Invoke-RestMethod -Uri "$BaseUrl/api/orders/$($checkout.orderId)" -Headers $customerHeaders
    if ($order.status -eq "Cancelled" -and $order.sagaStatus -ne "Compensating") { break }
    Start-Sleep -Seconds 2
}
if ($order.status -ne "Cancelled") { throw "Expected Cancelled but received $($order.status). Is PAYMENT_OUTCOME=Failure?" }

$payment = Invoke-RestMethod -Uri "$BaseUrl/api/payments/orders/$($checkout.orderId)" -Headers $adminHeaders
$after = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/$productId" -Headers $adminHeaders
if ($payment.status -ne "Failed") { throw "Expected failed payment but received $($payment.status)." }
if ($after.reservedQuantity -ne 0 -or $after.quantityOnHand -ne $before.quantityOnHand) {
    throw "Compensation did not release inventory without deducting on-hand stock."
}

Write-Host "Payment-failure compensation passed: order=$($order.id), stock=$($after.quantityOnHand)."
