import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Accept": "application/json"
}


def test_get_api_brand_comparison_valid_and_invalid_brands():
    # Test valid brand comparison parameters
    valid_params_list = [
        {"brand1": "hiq", "brand2": "ssn"},
        {"brand1": "ssn", "brand2": "hiq"}
    ]

    for params in valid_params_list:
        response = requests.get(
            f"{BASE_URL}/api/brand-comparison",
            headers=HEADERS,
            params=params,
            timeout=TIMEOUT
        )
        assert response.status_code == 200, f"Expected 200 for params {params}, got {response.status_code}"
        json_data = response.json()
        assert isinstance(json_data, dict), f"Expected JSON object for valid brands {params}, got {type(json_data)}"
        # Expect some data keys typical for comparison (cannot be sure, so at least non-empty)
        assert json_data, f"Expected non-empty data for valid brands {params}"

    # Test invalid or unknown brand parameters, expecting 400 or 404
    invalid_params_list = [
        {"brand1": "hiq", "brand2": "unknown-brand"},
        {"brand1": "unknown-brand-1", "brand2": "unknown-brand-2"},
        {"brand1": "hiq"},  # Missing brand2
        {"brand2": "ssn"},  # Missing brand1
        {},  # Missing both brand1 and brand2
        {"brand1": "", "brand2": ""},  # Empty strings
        {"brand1": "   ", "brand2": "ssn"}  # Whitespace as brand1
    ]

    for params in invalid_params_list:
        response = requests.get(
            f"{BASE_URL}/api/brand-comparison",
            headers=HEADERS,
            params=params,
            timeout=TIMEOUT
        )
        assert response.status_code in (400, 404), (
            f"Expected 400 or 404 for invalid params {params}, got {response.status_code}"
        )


test_get_api_brand_comparison_valid_and_invalid_brands()