import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_products_sparklines_with_valid_ids_and_days():
    headers = {
        "Accept": "application/json"
    }
    try:
        # Step 1: Fetch product IDs to get valid product IDs for the sparklines test
        products_resp = requests.get(
            f"{BASE_URL}/api/products",
            params={"page": 1, "pageSize": 100, "onlyDiscounted": "false"},
            headers=headers,
            timeout=TIMEOUT
        )
        assert products_resp.status_code == 200, f"Expected 200 but got {products_resp.status_code} when fetching products"
        products_data = products_resp.json()
        product_items = products_data.get("items") or products_data.get("data") or products_data
        assert isinstance(product_items, list), "Products response 'items' or 'data' should be a list"

        # Collect up to 100 product IDs
        product_ids = [str(product.get("id") or product.get("productId") or product.get("ID")) for product in product_items if product.get("id") or product.get("productId") or product.get("ID")]
        product_ids = product_ids[:100]
        assert product_ids, "No valid product IDs found to test sparklines endpoint"

        # Step 2: Call /api/products/sparklines with the valid product ids and a valid days parameter (example: 7)
        params = []
        for pid in product_ids:
            params.append(("ids[]", pid))
        params.append(("days", 7))

        sparklines_resp = requests.get(
            f"{BASE_URL}/api/products/sparklines",
            params=params,
            headers=headers,
            timeout=TIMEOUT
        )
        assert sparklines_resp.status_code == 200, f"Expected 200 but got {sparklines_resp.status_code} from sparklines endpoint"
        sparklines_data = sparklines_resp.json()
        # Validate that sparklines_data contains sparkline data for the given product IDs
        assert isinstance(sparklines_data, dict) or isinstance(sparklines_data, list), "Sparklines response should be a dict or list"
        # If dict keyed by IDs, check keys contain product IDs
        if isinstance(sparklines_data, dict):
            found_ids = set(str(k) for k in sparklines_data.keys())
            assert found_ids.intersection(set(product_ids)), "No matching product IDs found in sparklines response"
        elif isinstance(sparklines_data, list):
            # If list of sparklines objects, check presence of id field
            for item in sparklines_data:
                assert "id" in item or "productId" in item, "Sparkline item missing product ID key"
    except requests.RequestException as e:
        assert False, f"HTTP request failed: {e}"

test_get_api_products_sparklines_with_valid_ids_and_days()