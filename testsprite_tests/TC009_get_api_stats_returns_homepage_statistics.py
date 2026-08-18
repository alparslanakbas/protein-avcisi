import requests

def test_get_api_stats_returns_homepage_statistics():
    base_url = "http://localhost:5156"
    endpoint = "/api/stats"
    url = base_url + endpoint
    headers = {
        "Accept": "application/json"
    }
    timeout = 30

    try:
        response = requests.get(url, headers=headers, timeout=timeout)
    except requests.RequestException as e:
        assert False, f"Request to {url} failed with exception: {e}"

    assert response.status_code == 200, f"Expected status code 200, got {response.status_code}"
    try:
        data = response.json()
    except ValueError:
        assert False, "Response is not valid JSON"

    # Basic checks on the response structure and keys typical for homepage statistics
    assert isinstance(data, dict), "Response JSON should be an object"

    # Check some expected keys which might be in homepage stats for initial site experience
    # Since PRD doesn't explicitly state exact keys of /api/stats response,
    # we validate presence of at least one meaningful statistical key and that value types make sense.
    # We look for keys that might represent homepage stats, such as counters or summaries.

    expected_keys = ["discountCount", "storeCount", "couponCount", "guideArticleCount", "activeUserCount"]
    # Not all keys may be present, but at least one should be present as a sanity check
    present_keys = [key for key in expected_keys if key in data]
    assert len(present_keys) > 0, f"Response JSON does not contain expected homepage statistical keys from {expected_keys}"

    # Check types for any found keys (if numeric, should be int >=0)
    for key in present_keys:
        value = data[key]
        assert isinstance(value, int), f"Value for '{key}' should be int, got {type(value)}"
        assert value >= 0, f"Value for '{key}' should be non-negative, got {value}"

test_get_api_stats_returns_homepage_statistics()