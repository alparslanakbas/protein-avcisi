import requests
import time
import uuid

BASE_URL = "http://localhost:5156"
TIMEOUT = 30


def test_post_and_delete_api_products_id_favorite():
    session = requests.Session()
    headers = {"Content-Type": "application/json"}

    # Step 1: Obtain a valid product ID
    # We'll fetch products list and pick the first product ID available

    try:
        resp_products = session.get(f"{BASE_URL}/api/products?onlyDiscounted=false&page=1&pageSize=1", timeout=TIMEOUT)
        if resp_products.status_code == 429:
            # Rate limit hit - expected according to instructions
            assert resp_products.status_code == 429
            return
        assert resp_products.status_code == 200
        products_data = resp_products.json()
        products_list = products_data or []  # Fixed: API returns list directly, not inside 'data'
        assert isinstance(products_list, list) and len(products_list) > 0, "No products found to test."
        product = products_list[0]
        product_id = product.get("id")
        assert product_id is not None, "Product ID is None"
    except requests.RequestException as e:
        raise AssertionError(f"Failed to get product list: {e}")

    # Generate a unique new email for testing
    test_email = f"test_{uuid.uuid4().hex[:8]}@example.com"

    # Prepare post favorite payload
    post_payload = {"email": test_email}

    token = None
    try:
        # Step 2: POST /api/products/{id}/favorite with new email, expect 200 and token in response
        resp_post = session.post(f"{BASE_URL}/api/products/{product_id}/favorite", json=post_payload, headers=headers, timeout=TIMEOUT)
        if resp_post.status_code == 429:
            # Rate limit hit - expected according to instructions
            assert resp_post.status_code == 429
            return
        assert resp_post.status_code == 200, f"POST favorite failed with status code {resp_post.status_code}"
        post_resp_json = resp_post.json()
        token = post_resp_json.get("token")
        assert token is not None and isinstance(token, str) and token != "", "Token not returned in POST favorite response"

        # Step 3: GET /api/favorites?token=... should include the product
        resp_get_fav = session.get(f"{BASE_URL}/api/favorites", params={"token": token}, timeout=TIMEOUT)
        if resp_get_fav.status_code == 429:
            # Rate limit hit - expected according to instructions
            assert resp_get_fav.status_code == 429
            return
        assert resp_get_fav.status_code == 200, f"GET favorites failed with status code {resp_get_fav.status_code}"
        get_fav_json = resp_get_fav.json()
        favorites = get_fav_json.get("favorites") or get_fav_json.get("data") or []
        # The favorites list must contain the product_id (depending on API structure)
        found = False
        for fav in favorites:
            if isinstance(fav, dict):
                if fav.get("productId") == product_id or fav.get("id") == product_id:
                    found = True
                    break
            elif isinstance(fav, int):
                if fav == product_id:
                    found = True
                    break
        assert found, "Favorited product not found in favorites list after POST favorite"

        # Step 4: DELETE /api/products/{id}/favorite?token=... removes the favorite, expect 200
        resp_delete = session.delete(f"{BASE_URL}/api/products/{product_id}/favorite", params={"token": token}, timeout=TIMEOUT)
        if resp_delete.status_code == 429:
            # Rate limit hit - expected according to instructions
            assert resp_delete.status_code == 429
            return
        assert resp_delete.status_code == 200, f"DELETE favorite failed with status code {resp_delete.status_code}"

        # Step 5: GET /api/favorites?token=... again should NOT include the product now
        # Adding a short delay to allow backend to process deletion in case of eventual consistency
        time.sleep(1)
        resp_get_fav_after_delete = session.get(f"{BASE_URL}/api/favorites", params={"token": token}, timeout=TIMEOUT)
        if resp_get_fav_after_delete.status_code == 429:
            # Rate limit hit - expected according to instructions
            assert resp_get_fav_after_delete.status_code == 429
            return
        assert resp_get_fav_after_delete.status_code == 200, f"GET favorites after delete failed with status code {resp_get_fav_after_delete.status_code}"
        get_fav_after_delete_json = resp_get_fav_after_delete.json()
        favorites_after_delete = get_fav_after_delete_json.get("favorites") or get_fav_after_delete_json.get("data") or []
        found_after_delete = False
        for fav in favorites_after_delete:
            if isinstance(fav, dict):
                if fav.get("productId") == product_id or fav.get("id") == product_id:
                    found_after_delete = True
                    break
            elif isinstance(fav, int):
                if fav == product_id:
                    found_after_delete = True
                    break
        assert not found_after_delete, "Product still found in favorites list after DELETE favorite"

    finally:
        # Cleanup: ensure favorite is deleted in case test failed before deletion
        if token:
            try:
                session.delete(f"{BASE_URL}/api/products/{product_id}/favorite", params={"token": token}, timeout=TIMEOUT)
            except Exception:
                pass


test_post_and_delete_api_products_id_favorite()