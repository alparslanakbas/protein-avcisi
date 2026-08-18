import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {"Content-Type": "application/json"}

def test_post_api_products_id_vote():
    session = requests.Session()
    product_id = None

    try:
        # Step 1: Get a valid product ID by fetching products list (limit to 1)
        params = {"onlyDiscounted": False, "pageSize": 1, "page": 1}
        resp = session.get(f"{BASE_URL}/api/products", params=params, timeout=TIMEOUT)
        assert resp.status_code == 200, f"Failed to get products list: {resp.status_code}"
        data = resp.json()
        assert isinstance(data, dict) and "items" in data, "Unexpected products response structure"
        items = data.get("items", [])
        assert len(items) > 0, "No products available to test"
        first_item = items[0]
        assert isinstance(first_item, dict), "First product item is not an object"
        assert "id" in first_item and first_item["id"] is not None, "Product ID missing or null in product item"
        product_id = first_item["id"]

        # Helper function to post vote and handle 429 (rate limiting)
        def post_vote(pid, helpful_value):
            json_data = {"helpful": helpful_value}
            url = f"{BASE_URL}/api/products/{pid}/vote"
            while True:
                response = session.post(url, json=json_data, headers=HEADERS, timeout=TIMEOUT)
                if response.status_code == 429:
                    # Rate limit hit, wait and retry (as per instructions, treat 429 as expected)
                    time.sleep(3)
                    continue
                return response

        # Step 2: POST vote with {"helpful": true} for valid product ID
        resp_true = post_vote(product_id, True)
        assert resp_true.status_code == 200, f"POST vote helpful=true failed: {resp_true.status_code}"

        # Step 3: POST vote with {"helpful": false} for valid product ID
        resp_false = post_vote(product_id, False)
        assert resp_false.status_code == 200, f"POST vote helpful=false failed: {resp_false.status_code}"

        # Step 4: POST vote with {"helpful": true} for a non-existent product ID (use unlikely large ID)
        non_existent_id = 999999999999
        resp_non_exist_true = post_vote(non_existent_id, True)
        assert resp_non_exist_true.status_code in (400, 404), (
            f"POST vote helpful=true for non-existent ID expected 404 or 400, got {resp_non_exist_true.status_code}"
        )

        # Step 5: POST vote with {"helpful": false} for a non-existent product ID
        resp_non_exist_false = post_vote(non_existent_id, False)
        assert resp_non_exist_false.status_code in (400, 404), (
            f"POST vote helpful=false for non-existent ID expected 404 or 400, got {resp_non_exist_false.status_code}"
        )

    finally:
        session.close()

test_post_api_products_id_vote()
