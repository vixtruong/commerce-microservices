#!/usr/bin/env sh
set -eu

BASE_URL="${BASE_URL:-http://localhost:8080}"
CUSTOMER_EMAIL="${CUSTOMER_EMAIL:-customer@commerce.local}"
CUSTOMER_PASSWORD="${CUSTOMER_PASSWORD:-Customer123!}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@commerce.local}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-Administrator123!}"

attempt=0
until curl --fail --silent "$BASE_URL/health/ready" >/dev/null; do
  attempt=$((attempt + 1))
  [ "$attempt" -lt 90 ] || { echo "Gateway readiness timed out." >&2; exit 1; }
  sleep 2
done

customer_token=$(curl --fail --silent -X POST "$BASE_URL/api/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$CUSTOMER_EMAIL\",\"password\":\"$CUSTOMER_PASSWORD\"}" | jq -r .accessToken)
admin_token=$(curl --fail --silent -X POST "$BASE_URL/api/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" | jq -r .accessToken)
products=$(curl --fail --silent "$BASE_URL/api/catalog/products")
product_id=$(printf '%s' "$products" | jq -r '.items[] | select(.sku == "MACBOOK-PRO-001") | .id' | head -n 1)
[ -n "$product_id" ] && [ "$product_id" != null ] || { echo "Seed product not found." >&2; exit 1; }

curl --fail --silent -X PUT "$BASE_URL/api/cart/items/$product_id" \
  -H "Authorization: Bearer $customer_token" -H 'Content-Type: application/json' -d '{"quantity":1}' >/dev/null
checkout=$(curl --fail --silent -X POST "$BASE_URL/api/orders/checkout" \
  -H "Authorization: Bearer $customer_token" -H 'Idempotency-Key: commerce-smoke-checkout-v1' \
  -H 'Content-Type: application/json' \
  -d '{"recipientName":"Commerce Customer","addressLine1":"1 Reference Architecture Way","city":"Hanoi","postalCode":"100000","countryCode":"VN"}')
order_id=$(printf '%s' "$checkout" | jq -r .orderId)
checkout_replay=$(curl --fail --silent -X POST "$BASE_URL/api/orders/checkout" \
  -H "Authorization: Bearer $customer_token" -H 'Idempotency-Key: commerce-smoke-checkout-v1' \
  -H 'Content-Type: application/json' \
  -d '{"recipientName":"Commerce Customer","addressLine1":"1 Reference Architecture Way","city":"Hanoi","postalCode":"100000","countryCode":"VN"}')
replayed_order_id=$(printf '%s' "$checkout_replay" | jq -r .orderId)
[ "$replayed_order_id" = "$order_id" ] || { echo "Checkout idempotency returned another order." >&2; exit 1; }

attempt=0
while :; do
  order=$(curl --fail --silent "$BASE_URL/api/orders/$order_id" -H "Authorization: Bearer $customer_token")
  status=$(printf '%s' "$order" | jq -r .status)
  case "$status" in Shipped|Delivered) break ;; esac
  attempt=$((attempt + 1))
  [ "$attempt" -lt 60 ] || { echo "Order stalled in $status." >&2; exit 1; }
  sleep 2
done

payment_status=$(curl --fail --silent "$BASE_URL/api/payments/orders/$order_id" \
  -H "Authorization: Bearer $admin_token" | jq -r .status)
[ "$payment_status" = Succeeded ] || { echo "Payment state: $payment_status" >&2; exit 1; }
curl --fail --silent "$BASE_URL/api/shipping/orders/$order_id" -H "Authorization: Bearer $admin_token" >/dev/null
reserved=$(curl --fail --silent "$BASE_URL/api/inventory/$product_id" \
  -H "Authorization: Bearer $admin_token" | jq -r .reservedQuantity)
[ "$reserved" = 0 ] || { echo "Inventory reservation remains: $reserved" >&2; exit 1; }

echo "Smoke test passed: order=$order_id status=$status payment=$payment_status"
