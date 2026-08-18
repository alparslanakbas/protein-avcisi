import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30


def test_get_go_productid_redirects_to_store():
    # Step 1: Get a valid product ID by fetching products list from /api/products (read-only endpoint)
    products_url = f"{BASE_URL}/api/products"
    try:
        response = requests.get(products_url, params={"onlyDiscounted": "false", "pageSize": 1}, timeout=TIMEOUT)
        assert response.status_code == 200, f"Failed to fetch products list, status: {response.status_code}"
        products_data = response.json()
        assert "items" in products_data and len(products_data["items"]) > 0, "No products found in response"
        product_item = products_data["items"][0]
        # Ensure 'productId' key exists
        assert "productId" in product_item, f"Product item missing 'productId' field: {product_item}"
        product_id = product_item["productId"]
        assert isinstance(product_id, int) or isinstance(product_id, str), "Invalid product ID format"
    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Error during fetching product list: {e}")

    # Step 2: Call GET /go/{productId} and expect a 302 redirect to store URL
    go_url = f"{BASE_URL}/go/{product_id}"
    try:
        # Using allow_redirects=False to catch the 302 status and Location header
        response = requests.get(go_url, allow_redirects=False, timeout=TIMEOUT)

        # If rate limited, treat 429 as expected result per instructions
        if response.status_code == 429:
            print("Received HTTP 429 Too Many Requests - expected due to rate limiting, test passed per instructions.")
            return

        assert response.status_code == 302, f"Expected 302 redirect, got {response.status_code}"

        # Validate Location header presence and format (not empty)
        location = response.headers.get("Location")
        assert location is not None and location != "", "Redirect Location header missing or empty"

        # Optional: Validate location appears to be a URL (basic check)
        assert location.startswith("http"), f"Redirect Location header does not appear to be a URL: {location}"

    except requests.exceptions.RequestException as e:
        raise AssertionError(f"Request error during GET /go/{product_id}: {e}")


test_get_go_productid_redirects_to_store()
