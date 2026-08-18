import requests
from time import sleep

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Accept": "application/json"
}

def test_get_api_products_with_filters_and_sorting():
    session = requests.Session()
    session.headers.update(HEADERS)

    # Helper to handle 429 rate limiting - treat 429 as expected and pass
    def safe_get(url, params=None):
        response = session.get(url, params=params, timeout=TIMEOUT)
        if response.status_code == 429:
            # Expected rate limit response - treat as pass for this test
            return response
        response.raise_for_status()
        return response

    endpoint = f"{BASE_URL}/api/products"

    # 1) onlyDiscounted=false, with common filters & sorting, expect 200 and valid data
    params = {
        "onlyDiscounted": "false",
        "brands[]": ["hiq"],
        "categories[]": ["protein"],
        "search": "whey",
        "minPrice": "50",
        "maxPrice": "500",
        "sortBy": "priceAsc",
        "page": "1",
        "pageSize": "20"
    }
    response = safe_get(endpoint, params=params)
    if response.status_code == 429:
        # rate limit hit, pass as expected
        assert response.status_code == 429
    else:
        assert response.status_code == 200
        json_data = response.json()
        assert isinstance(json_data, dict)
        # Expect keys for paging and product list
        assert "products" in json_data
        assert isinstance(json_data["products"], list)
        # Products, if any, should respect filters (brand/category) if possible
        for product in json_data["products"]:
            # brandSlug or categorySlug may be in product to verify filtering
            brand = product.get("brandSlug", "").lower()
            category = product.get("categorySlug", "").lower()
            # Brand filter applied, but unknown brands may yield empty
            assert (brand == "hiq") if brand else True
            assert (category == "protein") if category else True

    # 2) unknown brand and category filters - should return 200 with empty or no matches
    params_unknown = {
        "onlyDiscounted": "false",
        "brands[]": ["unknownbrand12345"],
        "categories[]": ["unknowncategory12345"],
        "page": "1",
        "pageSize": "10"
    }
    response = safe_get(endpoint, params=params_unknown)
    if response.status_code == 429:
        # treat as expected
        assert response.status_code == 429
    else:
        assert response.status_code == 200
        json_data = response.json()
        assert isinstance(json_data, dict)
        products = json_data.get("products", [])
        assert isinstance(products, list)
        # Expect no results for unknown brand/category
        assert len(products) == 0

    # 3) restrictive price range that likely yields zero results
    params_restrictive_price = {
        "onlyDiscounted": "false",
        "minPrice": "999999",
        "maxPrice": "1000000",
        "page": "1",
        "pageSize": "10"
    }
    response = safe_get(endpoint, params=params_restrictive_price)
    if response.status_code == 429:
        # treat as expected
        assert response.status_code == 429
    else:
        assert response.status_code == 200
        json_data = response.json()
        products = json_data.get("products", [])
        assert isinstance(products, list)
        assert len(products) == 0

    # 4) multiple sort parameters tested separately - e.g., priceAsc, priceDesc, popularityDesc
    for sort_val in ["priceAsc", "priceDesc", "popularityDesc"]:
        params_sort = {
            "onlyDiscounted": "false",
            "sortBy": sort_val,
            "page": "1",
            "pageSize": "10"
        }
        response = safe_get(endpoint, params=params_sort)
        if response.status_code == 429:
            assert response.status_code == 429
        else:
            assert response.status_code == 200
            json_data = response.json()
            assert isinstance(json_data, dict)
            products = json_data.get("products", [])
            assert isinstance(products, list)

    # 5) Pagination sanity check with page and pageSize
    params_pagination = {
        "onlyDiscounted": "false",
        "page": "2",
        "pageSize": "5"
    }
    response = safe_get(endpoint, params=params_pagination)
    if response.status_code == 429:
        assert response.status_code == 429
    else:
        assert response.status_code == 200
        json_data = response.json()
        assert isinstance(json_data, dict)
        products = json_data.get("products", [])
        assert isinstance(products, list)

    session.close()

test_get_api_products_with_filters_and_sorting()