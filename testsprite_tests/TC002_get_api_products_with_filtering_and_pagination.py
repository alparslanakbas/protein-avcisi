import requests

def test_get_api_products_with_filtering_and_pagination():
    base_url = "http://localhost:5156"
    endpoint = "/api/products"
    url = base_url + endpoint

    # Define query parameters based on the test case
    params = [
        ("onlyDiscounted", "false"),
        ("search", "protein"),
        ("brands[]", "hiq"),
        ("brands[]", "ssn"),
        ("categories[]", "protein-tozu"),
        ("categories[]", "vitamin"),
        ("minPrice", 50),
        ("maxPrice", 500),
        ("sortBy", "price_asc"),
        ("page", 1),
        ("pageSize", 10)
    ]

    headers = {
        "Accept": "application/json"
    }

    try:
        response = requests.get(url, params=params, headers=headers, timeout=30)
    except requests.RequestException as e:
        assert False, f"Request to {url} failed: {e}"

    # Validate HTTP status code is 200
    assert response.status_code == 200, f"Expected 200 OK but got {response.status_code}"

    try:
        data = response.json()
    except ValueError:
        assert False, "Response is not a valid JSON"

    # Validate the response contains expected keys and types
    # Expecting a list of products and pagination metadata typically
    assert isinstance(data, dict), "Response JSON root should be a dictionary"

    # Typical keys might include "items" or "products" and "pagination"
    products_key = None
    for key in ["items", "products", "data"]:
        if key in data:
            products_key = key
            break
    assert products_key is not None, "Response JSON should contain a products list key (items, products, or data)"

    products = data[products_key]
    assert isinstance(products, list), f"{products_key} should be a list"

    # Each product should have at minimum id, name, brand, category, price keys
    if products:
        product = products[0]
        assert isinstance(product, dict), "Each product should be a dictionary"
        # id field might be named 'id' or 'key'
        id_field = None
        for f in ["id", "key"]:
            if f in product:
                id_field = f
                break
        assert id_field is not None, "Product missing required identifier field 'id' or 'key'"
        required_fields = ["name", "price"]
        for field in required_fields:
            assert field in product, f"Product missing required field '{field}'"
        # brand and category are likely present too, check if present then type check
        if "brand" in product:
            assert isinstance(product["brand"], str), "Field 'brand' should be a string if present"
        if "category" in product:
            assert isinstance(product["category"], str), "Field 'category' should be a string if present"

    # Validate pagination keys if present
    pagination_keys = ["page", "pageSize", "totalCount", "totalPages"]
    pagination_present = any(key in data for key in pagination_keys)
    if pagination_present:
        for key in pagination_keys:
            if key in data:
                assert isinstance(data[key], int), f"Pagination field '{key}' should be an integer"


test_get_api_products_with_filtering_and_pagination()
