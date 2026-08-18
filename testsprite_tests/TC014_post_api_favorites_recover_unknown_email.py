import requests
import time

BASE_URL = "http://localhost:5156"
ENDPOINT = "/api/favorites/recover"
TIMEOUT = 30
HEADERS = {"Content-Type": "application/json"}

def test_post_api_favorites_recover_unknown_email():
    test_emails = [
        "unknown_email_for_testing@example.com",
        "registered_email@example.com",  # Assuming this might be registered or not (generic message should be same)
        "invalid_email_format"            # Also test if backend handles malformed emails gracefully
    ]

    for email in test_emails:
        payload = {"email": email}
        try:
            response = requests.post(
                BASE_URL + ENDPOINT,
                json=payload,
                headers=HEADERS,
                timeout=TIMEOUT
            )
        except requests.exceptions.RequestException as e:
            # Network or other request related error
            assert False, f"Request failed: {e}"

        # Handle rate limit 429 - expected and treated as pass per instructions
        if response.status_code == 429:
            # Rate limiting active, expected outcome; treat as pass
            continue

        if email == "invalid_email_format":
            # Expecting 400 for invalid email format
            assert response.status_code == 400, f"Expected 400 status for invalid email format but got {response.status_code} for email '{email}'"
            continue

        # Assert HTTP 200 for other valid calls
        assert response.status_code == 200, f"Unexpected status code {response.status_code} for email '{email}'"

        # The response should contain a generic message regardless of email existence
        # We check that the response json has either a message key or a similar indicator
        try:
            json_data = response.json()
        except ValueError:
            assert False, "Response is not in JSON format"

        # Acceptable generic responses examples: message, detail, info keys or similar
        generic_message_keys = ["message", "detail", "info", "status"]
        assert any(key in json_data for key in generic_message_keys), f"Response JSON does not contain expected message keys for email '{email}'"

        # Optionally check the message content is generic and not leaking info
        # Here just check that it is a non-empty string
        for key in generic_message_keys:
            if key in json_data:
                assert isinstance(json_data[key], str) and len(json_data[key].strip()) > 0, f"Message field '{key}' empty or invalid for email '{email}'"
                break

        # Sleep a few seconds to avoid hitting rate limit in fast runs
        time.sleep(3)

test_post_api_favorites_recover_unknown_email()
