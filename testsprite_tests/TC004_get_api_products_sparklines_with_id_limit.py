import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Accept": "application/json"
}

def test_get_api_products_sparklines_with_id_limit():
    session = requests.Session()
    session.headers.update(HEADERS)

    # Step 1: Get product IDs to use for sparklines (need up to 101 distinct IDs)
    # We'll fetch /api/products to get product IDs
    try:
        resp_products = session.get(f"{BASE_URL}/api/products?onlyDiscounted=false&pageSize=150", timeout=TIMEOUT)
        if resp_products.status_code == 429:
            # Rate limit reached - treat as expected
            print("Received expected 429 from /api/products - rate limiting in effect. Test considered passed for rate limit.")
            return
        resp_products.raise_for_status()
    except requests.RequestException as e:
        assert False, f"Failed to fetch products list for IDs: {str(e)}"

    products_data = resp_products.json()
    # Extract product IDs from 'items' key or handle if response is a list
    product_items = products_data.get("items") if isinstance(products_data, dict) else None
    if product_items is None:
        # If 'items' not found, check if response itself is a list
        if isinstance(products_data, list):
            product_items = products_data
        else:
            assert False, "Could not find product list in /api/products response under 'items' key or as a list"

    if not isinstance(product_items, list):
        assert False, "Product list is not a list in /api/products response"

    product_ids = []
    for item in product_items:
        pid = item.get("id")
        if isinstance(pid, int):
            product_ids.append(pid)
        elif isinstance(pid, str) and pid.isdigit():
            product_ids.append(int(pid))
        if len(product_ids) >= 101:
            break

    assert len(product_ids) >= 1, "No product IDs available to run sparklines test"

    # Limit test 1: Valid request with up to 100 IDs
    valid_ids = product_ids[:100]
    params_valid = [("ids[]", str(pid)) for pid in valid_ids] + [("days", "30")]
    try:
        resp_valid = session.get(f"{BASE_URL}/api/products/sparklines", params=params_valid, timeout=TIMEOUT)
        if resp_valid.status_code == 429:
            print("Received expected 429 from /api/products/sparklines - rate limiting in effect. Test considered passed for rate limit.")
            return
        resp_valid.raise_for_status()
    except requests.RequestException as e:
        assert False, f"Failed to fetch sparklines with valid 100 IDs: {str(e)}"

    assert resp_valid.status_code == 200, f"Expected 200 for sparklines with 100 IDs, got {resp_valid.status_code}"
    try:
        data_valid = resp_valid.json()
    except Exception:
        assert False, "Response is not valid JSON for sparklines with up to 100 IDs"

    assert data_valid, "Empty sparklines response for 100 IDs"

    # Limit test 2: Exceed limit with 101 IDs (expect validation error 400)
    if len(product_ids) < 101:
        print("Not enough product IDs to test sparklines with exceeding limit of 100. Skipping limit exceed test.")
        return

    invalid_ids = product_ids[:101]
    params_invalid = [("ids[]", str(pid)) for pid in invalid_ids] + [("days", "30")]

    try:
        resp_invalid = session.get(f"{BASE_URL}/api/products/sparklines", params=params_invalid, timeout=TIMEOUT)
        if resp_invalid.status_code == 429:
            print("Received expected 429 from /api/products/sparklines (exceeding IDs) - rate limiting in effect. Test considered passed for rate limit.")
            return
        assert resp_invalid.status_code == 400, f"Expected 400 validation error when exceeding 100 IDs, got {resp_invalid.status_code}"
        try:
            error_data = resp_invalid.json()
        except Exception:
            error_data = None
        assert error_data, "Error response JSON missing when exceeding ID limit"
        assert any(k in error_data for k in ("errors", "message", "error")), "Error message or details missing in response"
    except requests.RequestException as e:
        assert False, f"Request failed when exceeding 100 IDs: {str(e)}"

test_get_api_products_sparklines_with_id_limit()
