import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_products_price_history_with_valid_and_invalid_days():
    headers = {
        "Accept": "application/json"
    }

    # Step 1: Get a valid product ID from /api/products to test price history endpoint
    try:
        resp_products = requests.get(f"{BASE_URL}/api/products", params={"pageSize": 1, "page": 1}, headers=headers, timeout=TIMEOUT)
        assert resp_products.status_code == 200, f"Failed to fetch products list, status code {resp_products.status_code}"
        products_data = resp_products.json()
        assert "items" in products_data and isinstance(products_data["items"], list) and len(products_data["items"]) > 0, "Products list is empty or invalid"
        first_product = products_data["items"][0]
        assert isinstance(first_product, dict), "Product item is not a dictionary"
        # Try to get product id from 'id' or 'productId'
        if "id" in first_product:
            product_id = first_product["id"]
        elif "productId" in first_product:
            product_id = first_product["productId"]
        else:
            raise AssertionError("Product item missing 'id' or 'productId' key")
    except (requests.RequestException, AssertionError) as e:
        raise AssertionError(f"Setup failed to get a valid product ID: {e}")

    valid_days_values = [7, 15, 30, 180, 365]
    invalid_days_values = [0, -1, 999, "abc", "", None]

    # Test valid days parameter values
    for days in valid_days_values:
        try:
            resp = requests.get(
                f"{BASE_URL}/api/products/{product_id}/price-history",
                params={"days": days},
                headers=headers,
                timeout=TIMEOUT
            )
            assert resp.status_code == 200, f"Expected 200 for days={days} but got {resp.status_code}"
            resp_json = resp.json()
            # Basic checks on returned data structure: assume it contains a list or dict with history info
            assert resp_json is not None, f"Response JSON is None for days={days}"
        except (requests.RequestException, AssertionError) as e:
            raise AssertionError(f"Valid days test failed for days={days}: {e}")

    # Test invalid days parameter values
    for days in invalid_days_values:
        # Set days parameter only if not None or empty string
        params = {}
        if days is not None and days != "":
            params["days"] = days
        try:
            resp = requests.get(
                f"{BASE_URL}/api/products/{product_id}/price-history",
                params=params,
                headers=headers,
                timeout=TIMEOUT
            )
            assert resp.status_code == 400, f"Expected 400 for invalid days={days} but got {resp.status_code}"
        except requests.RequestException as e:
            raise AssertionError(f"Error occurred when testing invalid days={days}: {e}")

test_get_api_products_price_history_with_valid_and_invalid_days()
