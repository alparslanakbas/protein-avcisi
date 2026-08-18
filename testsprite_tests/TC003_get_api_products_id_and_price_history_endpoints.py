import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30


def test_get_api_products_id_and_price_history_endpoints():
    session = requests.Session()

    # Helper to get a valid product id
    try:
        resp_products = session.get(f"{BASE_URL}/api/products", params={"pageSize": 1}, timeout=TIMEOUT)
        assert resp_products.status_code == 200, f"Failed fetching products list, status {resp_products.status_code}"
        products_data = resp_products.json()
        # Adjust to accept 'items' or 'data' as list container
        product_list = None
        if "data" in products_data and isinstance(products_data["data"], list):
            product_list = products_data["data"]
        elif "items" in products_data and isinstance(products_data["items"], list):
            product_list = products_data["items"]
        else:
            # If the response itself is a list
            if isinstance(products_data, list):
                product_list = products_data
        assert product_list is not None, "Products list missing in response"
        assert len(product_list) > 0, "No products found to test with"
        assert "id" in product_list[0], "First product in list has no 'id' field"
        valid_product_id = product_list[0]["id"]
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching product list: {e}")

    # 1) Verify GET /api/products/{id} returns product details (200) for valid ID
    try:
        url = f"{BASE_URL}/api/products/{valid_product_id}"
        resp = session.get(url, timeout=TIMEOUT)
        assert resp.status_code == 200, f"GET /api/products/{valid_product_id} returned {resp.status_code} instead of 200"
        data = resp.json()
        assert isinstance(data, dict), "Response is not a JSON object"
        assert data.get("id") == valid_product_id, "Product ID in response does not match requested ID"
        # Check presence of price key in product detail
        assert "price" in data, "Product price information missing"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching product details: {e}")

    # 2) Verify GET /api/products/{id} returns 404 for unknown ID
    unknown_id = 9999999999  # assumed unknown
    try:
        url = f"{BASE_URL}/api/products/{unknown_id}"
        resp = session.get(url, timeout=TIMEOUT)
        assert resp.status_code == 404, f"GET /api/products/{unknown_id} expected 404 but got {resp.status_code}"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching unknown product details: {e}")

    # 3) Verify GET /api/products/{id} returns 404 for missing ID (empty path)
    try:
        url = f"{BASE_URL}/api/products/"
        resp = session.get(url, timeout=TIMEOUT)
        assert resp.status_code == 404, f"GET /api/products/ with missing ID expected 404 but got {resp.status_code}"
    except requests.exceptions.RequestException:
        pass

    # 4) Verify GET /api/products/{id}/price-history?days=7 returns price history for valid days
    valid_days = 7
    try:
        url = f"{BASE_URL}/api/products/{valid_product_id}/price-history"
        resp = session.get(url, params={"days": valid_days}, timeout=TIMEOUT)
        assert resp.status_code == 200, f"GET /api/products/{valid_product_id}/price-history?days={valid_days} returned {resp.status_code} instead of 200"
        data = resp.json()
        assert isinstance(data, dict), "Price history response is not a JSON object"
        # Expect keys like 'prices' or 'priceHistory' or 'history'
        assert any(k in data for k in ("prices", "priceHistory", "history")), "Price history data missing keys"
        history_list = data.get("prices") or data.get("priceHistory") or data.get("history")
        assert isinstance(history_list, list), "Price history data is not a list"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching price history of product: {e}")

    # 5) Verify GET /api/products/{id}/price-history with invalid days returns validation error 400
    invalid_days = 999
    try:
        url = f"{BASE_URL}/api/products/{valid_product_id}/price-history"
        resp = session.get(url, params={"days": invalid_days}, timeout=TIMEOUT)
        assert resp.status_code == 400, f"GET /api/products/{valid_product_id}/price-history?days={invalid_days} expected 400 but got {resp.status_code}"
        err_data = resp.json()
        assert isinstance(err_data, dict), "Error response is not a JSON object"
        assert any(k in err_data for k in ("error", "message", "errors")), "Error message missing in validation error response"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching price history with invalid days: {e}")

    # 6) Verify GET /api/products/{id}/price-history with missing days param still works (200 or 400 accepted)
    try:
        url = f"{BASE_URL}/api/products/{valid_product_id}/price-history"
        resp = session.get(url, timeout=TIMEOUT)
        assert resp.status_code in (200, 400), f"GET /api/products/{valid_product_id}/price-history missing days param returned unexpected {resp.status_code}"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error fetching price history with missing days param: {e}")


test_get_api_products_id_and_price_history_endpoints()
