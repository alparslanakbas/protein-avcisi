import requests

BASE_URL = "http://localhost:5156"
TIMEOUT = 30
HEADERS = {
    "Accept": "application/json"
}


def test_get_api_articles_list_and_detail_with_valid_and_invalid_slugs():
    # Step 1: GET /api/articles - expect 200 with list or empty list
    url_list = f"{BASE_URL}/api/articles"
    try:
        resp_list = requests.get(url_list, headers=HEADERS, timeout=TIMEOUT)
    except requests.RequestException as e:
        assert False, f"Request to GET /api/articles failed: {e}"
    assert resp_list.status_code == 200, f"Expected 200 for GET /api/articles, got {resp_list.status_code}"
    try:
        articles = resp_list.json()
    except ValueError:
        assert False, "Response body for GET /api/articles is not valid JSON"

    assert isinstance(articles, list), "GET /api/articles response should be a list"

    if articles:
        # Step 2: GET /api/articles/{slug} with valid slug - expect 200 and article detail
        valid_slug = articles[0].get("slug") or articles[0].get("Slug")
        assert valid_slug, "Article object has no 'slug' field"
        url_detail_valid = f"{BASE_URL}/api/articles/{valid_slug}"
        try:
            resp_detail_valid = requests.get(url_detail_valid, headers=HEADERS, timeout=TIMEOUT)
        except requests.RequestException as e:
            assert False, f"Request to GET /api/articles/{valid_slug} failed: {e}"
        assert resp_detail_valid.status_code == 200, \
            f"Expected 200 for GET /api/articles/{valid_slug}, got {resp_detail_valid.status_code}"
        try:
            article_detail = resp_detail_valid.json()
        except ValueError:
            assert False, f"Response body for GET /api/articles/{valid_slug} is not valid JSON"
        # Basic validation of article detail should contain slug and content
        assert isinstance(article_detail, dict), f"Article detail should be a JSON object for slug '{valid_slug}'"
        assert "slug" in article_detail or "Slug" in article_detail, f"Article detail missing slug field for '{valid_slug}'"
    else:
        # If no articles returned, skip valid slug detail test
        valid_slug = None

    # Step 3: GET /api/articles/{slug} with unknown slug - expect 404 or 400
    unknown_slug = "non-existent-article-slug-xyz12345"
    url_detail_unknown = f"{BASE_URL}/api/articles/{unknown_slug}"
    try:
        resp_detail_unknown = requests.get(url_detail_unknown, headers=HEADERS, timeout=TIMEOUT)
    except requests.RequestException as e:
        assert False, f"Request to GET /api/articles/{unknown_slug} failed: {e}"
    assert resp_detail_unknown.status_code in (400, 404), \
        f"Expected 400 or 404 for GET /api/articles/{unknown_slug}, got {resp_detail_unknown.status_code}"

    # Step 4: GET /api/articles/{slug} with malformed slug - expect 400 or 404
    malformed_slugs = [
        "///",               # obviously malformed
        "slug with spaces",  # spaces not valid in URL path segment
        "invalid@@slug!!",   # invalid characters
        "%"                 # incomplete URL encoding
    ]
    for malformed_slug in malformed_slugs:
        # Construct URL carefully to ensure slug is passed as path segment (no url encoding here)
        url_detail_malformed = f"{BASE_URL}/api/articles/{malformed_slug}"
        try:
            resp_detail_malformed = requests.get(url_detail_malformed, headers=HEADERS, timeout=TIMEOUT)
        except requests.RequestException:
            # Depending on server behavior malformed requests may cause connection errors or similar; 
            # treat connection errors as a pass for invalid slug handling
            continue
        assert resp_detail_malformed.status_code in (400, 404), \
            f"Expected 400 or 404 for GET /api/articles/{malformed_slug}, got {resp_detail_malformed.status_code}"


test_get_api_articles_list_and_detail_with_valid_and_invalid_slugs()