import requests
from time import sleep

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_deals_with_various_filters_and_pagination():
    session = requests.Session()
    headers = {
        "Accept": "application/json",
    }

    # Define sets of parameters for success cases
    valid_queries = [
        # Basic pagination and defaults
        {},
        # Search term
        {"search": "protein"},
        # Brand filter (example brand slug)
        {"brands[]": "hiq"},
        # Category filter (example category slug)
        {"categories[]": "protein-tozu"},
        # Price range filter
        {"minPrice": "50", "maxPrice": "200"},
        # Sort by ascending price
        {"sortBy": "price-asc"},
        # Sort by descending discount
        {"sortBy": "discount-desc"},
        # Pagination parameters
        {"page": "2", "pageSize": "10"},
        # Combination of filters
        {"search": "amino", "brands[]": "ssn", "categories[]": "bcaa", "minPrice": "30", "maxPrice": "150", "sortBy": "price-desc", "page": "1", "pageSize": "5"},
    ]

    # Test all valid queries
    for params in valid_queries:
        try:
            resp = session.get(
                f"{BASE_URL}/api/deals",
                params=params,
                headers=headers,
                timeout=TIMEOUT,
            )
        except requests.RequestException as e:
            assert False, f"Request Exception for params {params}: {e}"

        # For rate limit 429, consider as expected per instructions
        if resp.status_code == 429:
            # Expected rate limit hit - skip assertion error
            continue

        assert resp.status_code == 200, f"Unexpected status code {resp.status_code} for params {params}"

        # Validate structure of response body
        try:
            data = resp.json()
        except Exception:
            assert False, f"Response is not valid JSON for params {params}"

        # Validate response contains keys expected for deals endpoint
        # From PRD, it returns discounted product list + pagination
        assert isinstance(data, dict), f"Response is not a dict for params {params}"
        assert "items" in data, f"'items' key missing in response for params {params}"
        assert isinstance(data["items"], list), f"'items' is not a list for params {params}"
        assert "pagination" in data, f"'pagination' key missing in response for params {params}"
        pagination = data["pagination"]
        assert isinstance(pagination, dict), f"'pagination' is not a dict for params {params}"
        # pagination should have keys like page, pageSize, totalItems
        for key in ["page", "pageSize", "totalItems", "totalPages"]:
            assert key in pagination, f"'{key}' missing in pagination for params {params}"

        # If items present, check at least expected keys in an item (id, name, discount)
        if data["items"]:
            item = data["items"][0]
            assert isinstance(item, dict), f"Item is not dict for params {params}"
            assert "id" in item, f"'id' missing in item for params {params}"
            assert "name" in item, f"'name' missing in item for params {params}"
            # discount can be a key representing discount percent or amount
            discount_keys = ["discount", "discountPercent", "price", "oldPrice"]
            assert any(k in item for k in discount_keys), f"Discount-related key missing in item for params {params}"

    # Now test validation error cases
    error_queries = [
        # Invalid pageSize (string instead of int)
        {"pageSize": "invalid"},
        # Negative page number
        {"page": "-1"},
        # Unsupported sortBy value
        {"sortBy": "unsupported-sort"},
        # Non-numeric minPrice/maxPrice
        {"minPrice": "cheap", "maxPrice": "expensive"},
        # pageSize too large
        {"pageSize": "10001"},
    ]

    for params in error_queries:
        try:
            resp = session.get(
                f"{BASE_URL}/api/deals",
                params=params,
                headers=headers,
                timeout=TIMEOUT,
            )
        except requests.RequestException as e:
            assert False, f"Request Exception for invalid params {params}: {e}"

        if resp.status_code == 429:
            # Rate limit hit expected if this happens - do not fail test
            continue

        # Expecting validation error 400
        assert resp.status_code == 400, f"Expected 400 status for invalid params {params}, got {resp.status_code}"

        # Validate error response body contains error info
        try:
            data = resp.json()
        except Exception:
            assert False, f"Response is not valid JSON for invalid params {params}"

        # Expect a validation error structure (e.g. error keys)
        assert isinstance(data, dict), f"Error response is not dict for invalid params {params}"
        assert "errors" in data or "message" in data, f"No error info in response for invalid params {params}"

    session.close()

test_get_api_deals_with_various_filters_and_pagination()