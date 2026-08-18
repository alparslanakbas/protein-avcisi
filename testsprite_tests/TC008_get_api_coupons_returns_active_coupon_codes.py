import requests

def test_get_api_coupons_returns_active_coupon_codes():
    base_url = "http://localhost:5156"
    url = f"{base_url}/api/coupons"
    headers = {
        "Accept": "application/json"
    }
    try:
        response = requests.get(url, headers=headers, timeout=30)
        response.raise_for_status()
    except requests.RequestException as e:
        assert False, f"Request to {url} failed: {e}"

    assert response.status_code == 200, f"Expected status code 200, got {response.status_code}"

    try:
        coupons = response.json()
    except ValueError:
        assert False, "Response is not in JSON format"

    assert isinstance(coupons, list), f"Expected response to be a list, got {type(coupons)}"

    # Each coupon in the list, if any, should at least be a dict with some keys, but no strict schema provided
    for coupon in coupons:
        assert isinstance(coupon, dict), f"Expected each coupon to be a dict, got {type(coupon)}"

test_get_api_coupons_returns_active_coupon_codes()