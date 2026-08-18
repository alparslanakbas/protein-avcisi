import requests

def test_get_api_deals_with_filtering_and_pagination():
    base_url = "http://localhost:5156"
    endpoint = "/api/deals"
    headers = {
        "Accept": "application/json"
    }
    params = {
        "onlyDiscounted": "true",
        "search": "protein",
        "brands": ["hiq", "ssn"],
        "categories": ["whey", "bcaa"],
        "minPrice": "50",
        "maxPrice": "200",
        "sortBy": "priceAsc",
        "page": "1",
        "pageSize": "10"
    }
    # Convert list params to repeated query parameters e.g. brands=hiq&brands=ssn
    query_params = []
    for key, value in params.items():
        if isinstance(value, list):
            for v in value:
                query_params.append((key, v))
        else:
            query_params.append((key, value))

    try:
        response = requests.get(
            url=f"{base_url}{endpoint}",
            headers=headers,
            params=query_params,
            timeout=30
        )
        # Assert status code
        assert response.status_code == 200, f"Expected status 200, got {response.status_code}"
        data = response.json()
        # Validate structure and content of response
        assert isinstance(data, dict), "Response JSON should be a dictionary"

        # Expecting keys like: items (list), pagination object/page info
        assert "items" in data, "'items' key missing in response"
        assert isinstance(data["items"], list), "'items' should be a list"

        # Pagination usually under 'pagination' key
        if "pagination" in data:
            pagination = data["pagination"]
            pagination_keys = ["page", "pageSize", "totalItems", "totalPages"]
            for key in pagination_keys:
                assert key in pagination, f"'{key}' key missing in pagination response"
        else:
            # If no 'pagination' key, verify top-level pagination keys
            pagination_keys = ["page", "pageSize", "totalItems", "totalPages"]
            for key in pagination_keys:
                assert key in data, f"'{key}' key missing in response"

        # Assert items have discounted prices (since onlyDiscounted=true)
        for item in data["items"]:
            # Each item expected to have 'discountedPrice' or similar field indicating discount
            # We check at least one price field and discounted price less than or equal original
            assert "price" in item or "originalPrice" in item, "Item missing price fields"
            # discounted price should be <= price/originalPrice, if discounted price exists
            discounted_price = item.get("discountedPrice")
            original_price = item.get("price") or item.get("originalPrice")
            if discounted_price is not None and original_price is not None:
                assert discounted_price <= original_price, "Discounted price is not less than or equal to original price"

        # Validate sorting by price ascending if items count > 1
        prices = []
        for item in data["items"]:
            price = item.get("discountedPrice") or item.get("price") or item.get("originalPrice")
            if price is not None:
                prices.append(price)
        # Prices should be ascending or equal
        assert prices == sorted(prices), "Items are not sorted by price ascending as requested"

    except requests.exceptions.RequestException as e:
        assert False, f"Request failed: {e}"
    except ValueError as e:
        assert False, f"Response is not valid JSON: {e}"

test_get_api_deals_with_filtering_and_pagination()
