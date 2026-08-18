import requests
import time

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Content-Type": "application/json"
}

def test_post_api_subscribe_valid_and_invalid_email():
    valid_email_payload = {"email": "valid.email@example.com"}
    invalid_email_payload = {"email": "not-an-email"}

    # Test valid email subscription, expect 200 and generic confirmation message
    try:
        response_valid = requests.post(
            f"{BASE_URL}/api/subscribe",
            json=valid_email_payload,
            headers=HEADERS,
            timeout=TIMEOUT,
        )
    except requests.RequestException as e:
        assert False, f"Request to /api/subscribe with valid email failed: {str(e)}"

    if response_valid.status_code == 429:
        # Rate limit hit, consider as expected per instructions
        print("Received expected 429 Too Many Requests for valid email subscription")
    else:
        assert response_valid.status_code == 200, f"Expected status 200 for valid email, got {response_valid.status_code}"
        json_valid = response_valid.json()
        assert isinstance(json_valid, dict), "Response body for valid email must be a JSON object"
        # The exact confirmation message is not asserted per instructions, just check for presence of keys or type
        assert any(
            key in json_valid for key in ("message", "detail", "status")
        ), "Response JSON should contain confirmation message keys"

    # Sleep a bit to avoid rate limit for next request
    time.sleep(3)

    # Test invalid email subscription, expect 400
    try:
        response_invalid = requests.post(
            f"{BASE_URL}/api/subscribe",
            json=invalid_email_payload,
            headers=HEADERS,
            timeout=TIMEOUT,
        )
    except requests.RequestException as e:
        assert False, f"Request to /api/subscribe with invalid email failed: {str(e)}"

    if response_invalid.status_code == 429:
        # Rate limit hit, consider as expected per instructions
        print("Received expected 429 Too Many Requests for invalid email subscription")
    else:
        assert response_invalid.status_code == 400, f"Expected status 400 for invalid email, got {response_invalid.status_code}"
        json_invalid = response_invalid.json()
        assert isinstance(json_invalid, dict), "Response body for invalid email must be a JSON object"
        # Expect some error detail in response for validation failure
        assert any(
            key in json_invalid for key in ("error", "errors", "message", "detail")
        ), "Response JSON should contain error details for invalid email"

test_post_api_subscribe_valid_and_invalid_email()