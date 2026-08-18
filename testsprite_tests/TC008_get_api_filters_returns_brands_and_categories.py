import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30

def test_get_api_filters_returns_brands_and_categories():
    url = f"{BASE_URL}/api/filters"
    headers = {
        "Accept": "application/json"
    }

    try:
        response = requests.get(url, headers=headers, timeout=TIMEOUT)
    except requests.RequestException as e:
        assert False, f"Request to {url} failed with exception: {e}"

    assert response.status_code == 200, f"Expected status code 200 but got {response.status_code}"
    try:
        data = response.json()
    except ValueError:
        assert False, "Response is not valid JSON"

    # Response JSON should contain at least 'brands' and 'categories' keys
    assert isinstance(data, dict), "Response JSON is not an object"
    assert "brands" in data, "'brands' key not found in response"
    assert "categories" in data, "'categories' key not found in response"

    # 'brands' and 'categories' should be lists (can be empty)
    assert isinstance(data["brands"], list), "'brands' is not a list"
    assert isinstance(data["categories"], list), "'categories' is not a list"

    # We accept empty lists as valid valid state (as per requirement)

test_get_api_filters_returns_brands_and_categories()