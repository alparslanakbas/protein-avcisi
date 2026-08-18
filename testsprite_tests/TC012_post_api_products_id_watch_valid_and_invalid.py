import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Content-Type": "application/json"
}

def test_post_api_products_id_watch_valid_and_invalid():
    session = requests.Session()
    session.headers.update(HEADERS)

    # Step 1: Get a valid product ID by listing products
    try:
        resp_products = session.get(f"{BASE_URL}/api/products", params={"onlyDiscounted": False, "page":1, "pageSize":1}, timeout=TIMEOUT)
    except requests.RequestException as e:
        assert False, f"Failed to get product list due to exception: {e}"

    assert resp_products.status_code == 200, f"Expected 200 from /api/products but got {resp_products.status_code}"
    try:
        products_data = resp_products.json()
    except Exception as e:
        assert False, f"Response is not valid JSON: {e}"

    valid_product_id = None
    if isinstance(products_data, dict) and "items" in products_data and isinstance(products_data["items"], list) and len(products_data["items"]) > 0:
        valid_product_id = products_data["items"][0].get("id")
    assert valid_product_id is not None, "No valid product ID found in /api/products response"

    test_email = "test@example.com"
    payload = {"email": test_email}

    def post_watch(product_id):
        try:
            return session.post(f"{BASE_URL}/api/products/{product_id}/watch", json=payload, timeout=TIMEOUT)
        except requests.RequestException as e:
            assert False, f"POST /api/products/{product_id}/watch request failed with exception: {e}"

    # Due to rate limiting: handle possible 429 by considering it a pass per instructions
    # POST with valid product id
    resp_valid = post_watch(valid_product_id)
    if resp_valid.status_code == 429:
        # Rate limited, treat as expected
        return
    assert resp_valid.status_code == 200, f"Expected 200 for valid product watch but got {resp_valid.status_code}"
    try:
        resp_valid.json()
    except Exception as e:
        assert False, f"Response to valid watch POST is not valid JSON: {e}"

    # POST with invalid (non-existent) product id
    invalid_product_id = "00000000-0000-0000-0000-000000000000"
    resp_invalid = post_watch(invalid_product_id)
    if resp_invalid.status_code == 429:
        # Rate limited, treat as expected
        return
    assert resp_invalid.status_code == 404, f"Expected 404 for non-existent product watch but got {resp_invalid.status_code}"
    try:
        resp_invalid.json()
    except Exception as e:
        assert False, f"Response to invalid watch POST is not valid JSON: {e}"

test_post_api_products_id_watch_valid_and_invalid()