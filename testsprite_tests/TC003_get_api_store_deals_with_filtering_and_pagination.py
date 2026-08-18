import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_store_deals_with_filtering_and_pagination():
    url = f"{BASE_URL}/api/store-deals"
    headers = {
        "Accept": "application/json",
    }
    params = {
        "search": "protein",
        "categories[]": ["supplements", "vitamins"],
        "sortBy": "priceAsc",
        "page": 1,
        "pageSize": 10
    }

    try:
        response = requests.get(url, headers=headers, params=params, timeout=TIMEOUT)
        assert response.status_code == 200, f"Expected 200 OK but got {response.status_code}"
        data = response.json()
        assert isinstance(data, dict), "Response JSON should be a dictionary"
        assert "items" in data, "Response should contain 'items' key"
        assert isinstance(data["items"], list), "'items' should be a list"
        assert "pagination" in data, "Response should contain 'pagination' key"
        pagination = data["pagination"]
        assert isinstance(pagination, dict), "'pagination' should be a dictionary"
        assert pagination.get("page") == 1, "'page' in pagination should be 1"
        assert pagination.get("pageSize") == 10, "'pageSize' in pagination should be 10"
        assert all(any(cat in params["categories[]"] for cat in deal.get("categories", [])) for deal in data["items"]) or len(data["items"])==0, "Deals should contain at least one of the requested categories"
        # Additional optional checks on items content if exists
        for deal in data["items"]:
            assert "store" in deal, "Each deal should have 'store' key"
            assert "price" in deal, "Each deal should have 'price' key"
    except requests.RequestException as e:
        assert False, f"Request failed: {e}"

test_get_api_store_deals_with_filtering_and_pagination()