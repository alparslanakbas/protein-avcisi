import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30


def test_get_api_products_by_id_with_valid_and_invalid_ids():
    headers = {
        "Accept": "application/json"
    }

    # Step 1: Get a valid product id by fetching the product list
    try:
        list_response = requests.get(
            f"{BASE_URL}/api/products",
            params={"page": 1, "pageSize": 1, "onlyDiscounted": "false"},
            headers=headers,
            timeout=TIMEOUT
        )
        assert list_response.status_code == 200, f"Failed to get product list, status code: {list_response.status_code}"
        list_data = list_response.json()
        assert isinstance(list_data, dict), "Product list response is not a dictionary"
        products = list_data.get("items") or list_data.get("data") or list_data  # Support different structures
        if not products or len(products) == 0:
            raise AssertionError("No products found in product list to test with a valid ID")
        # Extract product id
        if isinstance(products, list):
            product_id = products[0].get("id") or products[0].get("productId") or products[0].get("Id")
        elif isinstance(products, dict):
            product_id = products.get("id") or products.get("productId") or products.get("Id")
        else:
            raise AssertionError("Unexpected product list structure")
        assert product_id is not None, "Product ID not found in product list item"
    except (requests.RequestException, AssertionError) as e:
        raise RuntimeError(f"Setup failed: Could not retrieve a valid product ID. Details: {e}")

    # Step 2: Test GET /api/products/{valid_id} returns 200 with product detail
    valid_response = requests.get(
        f"{BASE_URL}/api/products/{product_id}",
        headers=headers,
        timeout=TIMEOUT
    )
    assert valid_response.status_code == 200, f"Expected 200 status for valid product ID, got {valid_response.status_code}"
    # Check json structure for product detail, latch to id field
    try:
        product_detail = valid_response.json()
    except Exception:
        raise AssertionError("Response is not valid JSON for valid product ID")
    assert isinstance(product_detail, dict), "Product detail response is not a JSON object"
    # Check that product id in response matches requested id (loosely)
    resp_id = product_detail.get("id") or product_detail.get("productId") or product_detail.get("Id")
    assert resp_id == product_id, f"Returned product ID {resp_id} does not match requested ID {product_id}"

    # Step 3: Test GET /api/products/{invalid_id} returns 404
    invalid_id = "00000000-0000-0000-0000-000000000000"
    invalid_response = requests.get(
        f"{BASE_URL}/api/products/{invalid_id}",
        headers=headers,
        timeout=TIMEOUT
    )
    assert invalid_response.status_code == 404, f"Expected 404 status for non-existent product ID, got {invalid_response.status_code}"


test_get_api_products_by_id_with_valid_and_invalid_ids()