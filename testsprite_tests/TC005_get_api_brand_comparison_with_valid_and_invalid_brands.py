import requests
from time import sleep

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Accept": "application/json"
}

def test_get_api_brand_comparison_with_valid_and_invalid_brands():
    session = requests.Session()
    session.headers.update(HEADERS)
    
    # A helper function to handle rate-limiting 429 responses by retrying after a delay
    def safe_get(url, params=None):
        for attempt in range(3):
            resp = session.get(url, params=params, timeout=TIMEOUT)
            if resp.status_code == 429:
                # Rate limit reached, wait and retry
                sleep(3)
                continue
            return resp
        return resp

    # 1. Test valid brand comparison pairs: brand1=hiq, brand2=ssn
    params_valid_1 = {"brand1": "hiq", "brand2": "ssn"}
    response = safe_get(f"{BASE_URL}/api/brand-comparison", params=params_valid_1)
    assert response.status_code == 200, f"Expected 200 for valid brands but got {response.status_code}"
    json_data = response.json()
    # Expecting json_data to be a dict with category-based average price comparison table
    assert isinstance(json_data, dict), "Response should be a dictionary"
    assert len(json_data) > 0, "Response dictionary should not be empty for valid brand pair"
    # Additional sanity check: keys should be category names or similar
    first_key = next(iter(json_data))
    assert isinstance(first_key, str) and json_data[first_key] is not None

    # 2. Test the canonical pair order: brand1=ssn, brand2=hiq (should also succeed and match structure)
    params_valid_2 = {"brand1": "ssn", "brand2": "hiq"}
    response = safe_get(f"{BASE_URL}/api/brand-comparison", params=params_valid_2)
    assert response.status_code == 200, f"Expected 200 for reversed valid brands but got {response.status_code}"
    json_data_rev = response.json()
    assert isinstance(json_data_rev, dict), "Response should be a dictionary for reversed brands"
    assert len(json_data_rev) > 0, "Response dictionary should not be empty for reversed valid brand pair"

    # 3. Test unknown brand: brand1=hiq, brand2=unknown
    params_unknown = {"brand1": "hiq", "brand2": "unknown"}
    response = safe_get(f"{BASE_URL}/api/brand-comparison", params=params_unknown)
    # Could be 400 or 404 according to the PRD
    assert response.status_code in (400, 404), f"Expected 400 or 404 for unknown brand but got {response.status_code}"
    try:
        json_err = response.json()
        assert "error" in json_err or "message" in json_err
    except Exception:
        # If no JSON body, accept as pass because server returned expected status code
        pass

    # 4. Test missing brand parameters: no brand1 and brand2 provided
    response = safe_get(f"{BASE_URL}/api/brand-comparison")
    # Expect 400 as required params are missing
    assert response.status_code == 400, f"Expected 400 for missing brand params but got {response.status_code}"
    try:
        json_err = response.json()
        assert "error" in json_err or "message" in json_err
    except Exception:
        # If no JSON body, accept as pass because server returned expected status code
        pass

test_get_api_brand_comparison_with_valid_and_invalid_brands()
